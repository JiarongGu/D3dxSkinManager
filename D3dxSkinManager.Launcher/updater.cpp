// Auto-update -- APPLY phase (the launcher's half of the two-phase update flow).
//
// The running app downloads + extracts the update itself and stages it under {install}/.update/
// (staged/ + ready.json), because a running .NET app can't replace its own exe. On the next startup
// the launcher -- which runs BEFORE the app -- applies that staged update here:
//   1. If {install}/.update/ready.json is absent, do nothing (no network, no prompt).
//   2. Otherwise overlay {install}/.update/staged over the install with robocopy, EXCEPT the launcher
//      itself, delete files dropped from the new manifest, and clear the staging dir.
// The launcher never checks GitHub or prompts -- that's the app's job (see UpdateService /
// docs/LAUNCHER_ARCHITECTURE.md). Non-fatal throughout: any failure still launches the app.

#include "updater.h"
#include <windows.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <string>
#include <set>
#include <fstream>
#include <sstream>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "shell32.lib")

namespace {

constexpr auto LAUNCHER_NAME = L"D3dxSkinManager Launcher.exe";

std::string ReadFileUtf8(const std::wstring& path)
{
    std::ifstream f(path, std::ios::binary);
    if (!f) return {};
    std::ostringstream ss;
    ss << f.rdbuf();
    return ss.str();
}

// Extract every "path": "value" entry (the manifest's file list), forward-slash normalized.
std::set<std::string> ExtractPaths(const std::string& json)
{
    std::set<std::string> out;
    const std::string needle = "\"path\"";
    size_t pos = 0;
    while ((pos = json.find(needle, pos)) != std::string::npos)
    {
        size_t colon = json.find(':', pos + needle.size());
        if (colon == std::string::npos) break;
        size_t q1 = json.find('"', colon + 1);
        if (q1 == std::string::npos) break;
        size_t q2 = json.find('"', q1 + 1);
        if (q2 == std::string::npos) break;
        out.insert(json.substr(q1 + 1, q2 - q1 - 1));
        pos = q2 + 1;
    }
    return out;
}

std::wstring Utf8ToW(const std::string& s)
{
    if (s.empty()) return {};
    int n = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), nullptr, 0);
    std::wstring w(n, 0);
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), &w[0], n);
    return w;
}

// Run a hidden process and wait; returns its exit code (or -1 on failure to start).
int RunHidden(const std::wstring& cmdLine)
{
    std::wstring mutableCmd = cmdLine;
    if (mutableCmd.empty()) return -1;
    STARTUPINFOW si = { sizeof(STARTUPINFOW) };
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    PROCESS_INFORMATION pi;
    if (!CreateProcessW(nullptr, &mutableCmd[0], nullptr, nullptr, FALSE,
                        CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
    {
        return -1;
    }
    WaitForSingleObject(pi.hProcess, INFINITE);
    DWORD code = 1;
    GetExitCodeProcess(pi.hProcess, &code);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
    return (int)code;
}

void DeleteDirRecursive(const std::wstring& dir)
{
    if (!PathFileExistsW(dir.c_str())) return;
    std::wstring from = dir;
    from.push_back(L'\0'); // double-null terminated for SHFileOperation
    SHFILEOPSTRUCTW op = {};
    op.wFunc = FO_DELETE;
    op.pFrom = from.c_str();
    op.fFlags = FOF_NO_UI | FOF_NOCONFIRMATION | FOF_SILENT;
    SHFileOperationW(&op);
}

HWND ShowStatus(const wchar_t* text)
{
    return CreateWindowW(L"STATIC", text, WS_VISIBLE | WS_POPUP | SS_CENTER,
                         (GetSystemMetrics(SM_CXSCREEN) - 420) / 2,
                         (GetSystemMetrics(SM_CYSCREEN) - 90) / 2,
                         420, 90, nullptr, nullptr, GetModuleHandle(nullptr), nullptr);
}

} // namespace

bool ApplyPendingUpdate(const std::wstring& appDirectory)
{
    const std::wstring stagingRoot = appDirectory + L"\\.update";
    const std::wstring readyMarker = stagingRoot + L"\\ready.json";
    const std::wstring stagedDir = stagingRoot + L"\\staged";

    // 1. Nothing staged -> nothing to do (no network, no prompt).
    if (!PathFileExistsW(readyMarker.c_str()))
        return true;

    // Marker without staged files -> corrupt; clear it so we don't loop.
    if (!PathFileExistsW(stagedDir.c_str()))
    {
        DeleteDirRecursive(stagingRoot);
        return true;
    }

    HWND status = ShowStatus(L"Installing update...\n\nThis will only take a moment.");

    // Read old + new manifests BEFORE the overlay (robocopy overwrites manifest.json).
    std::string oldManifest = ReadFileUtf8(appDirectory + L"\\manifest.json");
    std::string newManifest = ReadFileUtf8(stagedDir + L"\\manifest.json");

    // 2. Overlay staged files onto the install, EXCLUDING the launcher (it is running).
    //    robocopy /E mirrors subdirs; /XF excludes; exit codes < 8 = success.
    std::wstring copy = L"robocopy \"" + stagedDir + L"\" \"" + appDirectory +
        L"\" /E /XF \"" + LAUNCHER_NAME + L"\" /NJH /NJS /NP /NFL /NDL /R:3 /W:1";
    int rc = RunHidden(copy);
    bool ok = (rc >= 0 && rc < 8);

    if (ok)
    {
        // 3. Removals: files the old manifest listed but the new one no longer does (only tracked
        //    files; never the launcher or the manifest itself).
        std::set<std::string> oldPaths = ExtractPaths(oldManifest);
        std::set<std::string> newPaths = ExtractPaths(newManifest);
        for (const auto& p : oldPaths)
        {
            if (newPaths.count(p)) continue;
            std::wstring rel = Utf8ToW(p);
            for (auto& ch : rel) if (ch == L'/') ch = L'\\';
            std::wstring full = appDirectory + L"\\" + rel;
            DeleteFileW(full.c_str());
        }
    }

    // 4. Clear staging regardless (avoid re-applying on every launch).
    DeleteDirRecursive(stagingRoot);
    if (status) DestroyWindow(status);

    if (!ok)
    {
        MessageBoxW(nullptr,
            L"The downloaded update could not be applied. The current version will start instead.\n\n"
            L"It will be retried the next time an update is downloaded.",
            L"D3dxSkinManager - Update", MB_OK | MB_ICONWARNING);
    }
    return true;
}
