// Auto-update functionality implementation.
//
// Differential self-update driven by the release manifest (see docs/LAUNCHER_ARCHITECTURE.md):
//   1. Download the latest release manifest.json (GitHub's releases/latest/download/<asset> redirect).
//   2. Compare manifest.version to the locally-installed manifest.json.
//   3. If newer, prompt the user; on consent download the release zip, extract it, overlay all files
//      EXCEPT this launcher (a running exe can't replace itself), delete files dropped from the new
//      manifest, and refresh the local manifest.
//
// The launcher then proceeds to launch the (now-updated) D3dxSkinManager.exe (main.cpp). The main app
// exe is NOT running during this step, so it is safe to replace.

#include "updater.h"
#include <windows.h>
#include <urlmon.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <string>
#include <set>
#include <fstream>
#include <sstream>

#pragma comment(lib, "urlmon.lib")
#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "shell32.lib")

namespace {

constexpr auto REPO_BASE = L"https://github.com/JiarongGu/D3dxSkinManager/releases/latest/download/";
constexpr auto MANIFEST_NAME = L"manifest.json";
constexpr auto LAUNCHER_NAME = L"D3dxSkinManager Launcher.exe";

// ---- small helpers -------------------------------------------------------

std::wstring TempDir()
{
    wchar_t buf[MAX_PATH];
    GetTempPathW(MAX_PATH, buf);
    return std::wstring(buf);
}

std::string ReadFileUtf8(const std::wstring& path)
{
    std::ifstream f(path, std::ios::binary);
    if (!f) return {};
    std::ostringstream ss;
    ss << f.rdbuf();
    return ss.str();
}

// Extract the first JSON string value for "key": "value". Minimal scanner -- our manifest is generated
// (stable, pretty-printed) so this is sufficient; no general-purpose JSON parsing needed.
std::string ExtractJsonValue(const std::string& json, const std::string& key)
{
    std::string needle = "\"" + key + "\"";
    size_t k = json.find(needle);
    if (k == std::string::npos) return {};
    size_t colon = json.find(':', k + needle.size());
    if (colon == std::string::npos) return {};
    size_t q1 = json.find('"', colon + 1);
    if (q1 == std::string::npos) return {};
    size_t q2 = json.find('"', q1 + 1);
    if (q2 == std::string::npos) return {};
    return json.substr(q1 + 1, q2 - q1 - 1);
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

// Parse "X.Y[.Z]" into a comparable tuple; returns false if not parseable.
bool ParseVersion(const std::string& v, int& major, int& minor, int& patch)
{
    major = minor = patch = 0;
    int parts[3] = { 0, 0, 0 };
    int idx = 0;
    bool any = false;
    for (size_t i = 0; i < v.size() && idx < 3; ++i)
    {
        char c = v[i];
        if (c >= '0' && c <= '9') { parts[idx] = parts[idx] * 10 + (c - '0'); any = true; }
        else if (c == '.') { ++idx; }
        else if (c == 'v' || c == 'V') { continue; }
        else break;
    }
    major = parts[0]; minor = parts[1]; patch = parts[2];
    return any;
}

// true if 'latest' is strictly newer than 'local'.
bool IsNewer(const std::string& latest, const std::string& local)
{
    int la, li, lp, ca, ci, cp;
    if (!ParseVersion(latest, la, li, lp)) return false;
    if (!ParseVersion(local, ca, ci, cp)) return false;
    if (la != ca) return la > ca;
    if (li != ci) return li > ci;
    return lp > cp;
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
    // Double-null terminated path for SHFileOperation.
    std::wstring from = dir;
    from.push_back(L'\0');
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

bool CheckForUpdates(const std::wstring& appDirectory)
{
    const std::wstring localManifestPath = appDirectory + L"\\" + MANIFEST_NAME;

    // No local manifest -> can't diff (e.g. an older build). Skip rather than force a reinstall.
    std::string localJson = ReadFileUtf8(localManifestPath);
    if (localJson.empty()) return true;
    std::string localVersion = ExtractJsonValue(localJson, "version");
    if (localVersion.empty()) return true;

    // 1. Fetch the latest release manifest (redirects to the newest release's asset).
    const std::wstring tmp = TempDir();
    const std::wstring latestManifestPath = tmp + L"d3dx-latest-manifest.json";
    DeleteFileW(latestManifestPath.c_str());
    if (FAILED(URLDownloadToFileW(nullptr, (std::wstring(REPO_BASE) + MANIFEST_NAME).c_str(),
                                  latestManifestPath.c_str(), 0, nullptr)))
    {
        return true; // offline / no manifest published -- not fatal.
    }

    std::string latestJson = ReadFileUtf8(latestManifestPath);
    std::string latestVersion = ExtractJsonValue(latestJson, "version");
    if (latestVersion.empty() || !IsNewer(latestVersion, localVersion))
    {
        DeleteFileW(latestManifestPath.c_str());
        return true; // up to date.
    }

    // 2. Ask the user.
    std::wstring prompt = L"A new version of D3dxSkinManager is available.\n\n"
                          L"Installed: " + Utf8ToW(localVersion) +
                          L"\nLatest: " + Utf8ToW(latestVersion) +
                          L"\n\nUpdate now? (The app will start once the update finishes.)";
    if (MessageBoxW(nullptr, prompt.c_str(), L"D3dxSkinManager - Update Available",
                    MB_YESNO | MB_ICONINFORMATION) != IDYES)
    {
        DeleteFileW(latestManifestPath.c_str());
        return true; // user declined -- launch the current version.
    }

    HWND status = ShowStatus(L"Downloading update...\n\nThis may take a moment.");

    // 3. Download the release zip (asset name embeds the version).
    std::wstring zipName = L"D3dxSkinManager-v" + Utf8ToW(latestVersion) + L"-win-x64.zip";
    std::wstring zipUrl = std::wstring(REPO_BASE) + zipName;
    std::wstring zipPath = tmp + L"d3dx-update.zip";
    std::wstring stageDir = tmp + L"d3dx-update-stage";
    DeleteFileW(zipPath.c_str());
    DeleteDirRecursive(stageDir);

    bool ok = false;
    if (SUCCEEDED(URLDownloadToFileW(nullptr, zipUrl.c_str(), zipPath.c_str(), 0, nullptr)))
    {
        // 4. Extract via PowerShell Expand-Archive (no external zip lib needed; Win10+ ships it).
        std::wstring expand = L"powershell -NoProfile -ExecutionPolicy Bypass -Command "
            L"\"Expand-Archive -LiteralPath '" + zipPath + L"' -DestinationPath '" + stageDir + L"' -Force\"";
        if (RunHidden(expand) == 0 && PathFileExistsW(stageDir.c_str()))
        {
            // 5. Overlay every extracted file EXCEPT the launcher (it is running). robocopy /E mirrors
            //    subdirs; /XF excludes the launcher; exit codes < 8 = success.
            std::wstring copy = L"robocopy \"" + stageDir + L"\" \"" + appDirectory +
                L"\" /E /XF \"" + LAUNCHER_NAME + L"\" /NJH /NJS /NP /NFL /NDL /R:2 /W:1";
            int rc = RunHidden(copy);
            if (rc >= 0 && rc < 8)
            {
                // 6. Removals: files the old manifest listed but the new one no longer does
                //    (never the launcher or the manifest itself). Only touches files we tracked.
                std::set<std::string> oldPaths = ExtractPaths(localJson);
                std::set<std::string> newPaths = ExtractPaths(latestJson);
                for (const auto& p : oldPaths)
                {
                    if (newPaths.count(p)) continue;
                    std::wstring rel = Utf8ToW(p);
                    for (auto& ch : rel) if (ch == L'/') ch = L'\\';
                    std::wstring full = appDirectory + L"\\" + rel;
                    DeleteFileW(full.c_str());
                }
                ok = true;
            }
        }
    }

    // 7. Cleanup. The new manifest was copied in by robocopy (it's part of the zip), so the next
    //    launch sees the updated local version.
    DeleteDirRecursive(stageDir);
    DeleteFileW(zipPath.c_str());
    DeleteFileW(latestManifestPath.c_str());
    if (status) DestroyWindow(status);

    if (!ok)
    {
        MessageBoxW(nullptr,
            L"The update could not be applied. The current version will start instead.\n\n"
            L"You can download the latest version manually from the GitHub releases page.",
            L"D3dxSkinManager - Update Failed", MB_OK | MB_ICONWARNING);
    }
    return true;
}
