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
#include <tlhelp32.h>
#include <string>
#include <set>
#include <vector>
#include <cctype>
#include <fstream>
#include <sstream>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "shell32.lib")

namespace {

// Launcher basenames (lower-case) that must NEVER be deleted by the manifest-diff removal step. In the
// TOPOLOGY MIGRATION the old install's manifest lists "D3dxSkinManager.exe" as the APP; the new manifest
// excludes it (it is the launcher now), so the diff would otherwise delete the freshly-copied new
// launcher. Guard BOTH the new and the legacy launcher name.
const std::set<std::string> kLauncherBasenames = {
    "d3dxskinmanager.exe",
    "d3dxskinmanager launcher.exe",
};

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

// The running launcher's own file name (e.g. "D3dxSkinManager.exe", or the legacy
// "D3dxSkinManager Launcher.exe" during a migration apply). robocopy /XF excludes THIS so the launcher
// never tries to overwrite its own running image. Excluding only the OWN name (not both) is deliberate:
// in a migration the OLD launcher is running, so the staged NEW launcher (D3dxSkinManager.exe) is a
// different name and DOES get copied.
std::wstring OwnExeBasename()
{
    wchar_t path[MAX_PATH];
    GetModuleFileNameW(nullptr, path, MAX_PATH);
    return std::wstring(PathFindFileNameW(path));
}

// Force-close any running APP-RUNTIME instance under the install so the exe unlocks before the overlay.
// Single-instance already means ≤1 app, and the app that triggered the restart is exiting — this is the
// safety net for a HUNG/zombie instance (and makes robocopy deterministic instead of relying on retries).
// Targets the runtime image by FULL path (new: {install}\lib\D3dxSkinManager.App.exe; migration/old:
// {install}\D3dxSkinManager.exe) and skips our own PID (the new launcher shares that name).
void CloseRunningAppInstances(const std::wstring& installRoot)
{
    const std::wstring newRuntime = installRoot + L"\\lib\\D3dxSkinManager.App.exe";
    const std::wstring oldRuntime = installRoot + L"\\D3dxSkinManager.exe";
    const DWORD selfPid = GetCurrentProcessId();

    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE) return;

    std::vector<HANDLE> terminated;
    PROCESSENTRY32W pe = { sizeof(PROCESSENTRY32W) };
    if (Process32FirstW(snap, &pe))
    {
        do {
            if (pe.th32ProcessID == selfPid) continue;
            HANDLE hProc = OpenProcess(
                PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE | SYNCHRONIZE,
                FALSE, pe.th32ProcessID);
            if (!hProc) continue;

            wchar_t imgPath[MAX_PATH];
            DWORD sz = MAX_PATH;
            bool match = false;
            if (QueryFullProcessImageNameW(hProc, 0, imgPath, &sz))
            {
                if (_wcsicmp(imgPath, newRuntime.c_str()) == 0 ||
                    _wcsicmp(imgPath, oldRuntime.c_str()) == 0)
                {
                    match = true;
                }
            }
            if (match && TerminateProcess(hProc, 0))
            {
                terminated.push_back(hProc); // keep handle to wait for exit below
            }
            else
            {
                CloseHandle(hProc);
            }
        } while (Process32NextW(snap, &pe));
    }
    CloseHandle(snap);

    // Bounded wait so the killed processes' exe locks actually release before robocopy runs.
    for (HANDLE h : terminated)
    {
        WaitForSingleObject(h, 4000);
        CloseHandle(h);
    }
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

    // Close any running app-runtime instance so its exe unlocks before the overlay (safety net for a
    // hung/zombie instance; a normal restart already exited the app before starting us).
    CloseRunningAppInstances(appDirectory);

    // 2. Overlay staged files onto the install, EXCLUDING the RUNNING launcher's OWN image (it can't be
    //    overwritten while running, and never self-updates). Only the own name is excluded — so during a
    //    migration the staged NEW launcher (D3dxSkinManager.exe, a different name than the running OLD
    //    launcher) still copies. robocopy /E mirrors subdirs; /XF excludes; exit codes < 8 = success.
    std::wstring launcherName = OwnExeBasename();
    std::wstring copy = L"robocopy \"" + stagedDir + L"\" \"" + appDirectory +
        L"\" /E /XF \"" + launcherName + L"\" /NJH /NJS /NP /NFL /NDL /R:3 /W:1";
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

            // Never delete a launcher via the diff. Migration flips D3dxSkinManager.exe from APP (in the
            // old manifest) to LAUNCHER (absent from the new manifest), so without this guard the diff
            // would delete the just-copied new launcher. Compare the basename case-insensitively.
            std::string base = p;
            size_t slash = base.find_last_of("/\\");
            if (slash != std::string::npos) base = base.substr(slash + 1);
            for (auto& c : base) c = (char)std::tolower((unsigned char)c);
            if (kLauncherBasenames.count(base)) continue;

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
