// .NET Runtime detection, installation, and hosting implementation
#include "dotnet_runtime.h"
#include <windows.h>
#include <urlmon.h>
#include <shlwapi.h>
#include <string>

#pragma comment(lib, "urlmon.lib")
#pragma comment(lib, "shlwapi.lib")

constexpr auto REQUIRED_RUNTIME = L"Microsoft.WindowsDesktop.App";
constexpr auto REQUIRED_VERSION = L"10.0";
constexpr auto RUNTIME_URL_X64 = L"https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe";
constexpr auto RUNTIME_URL_X86 = L"https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x86.exe";

// Check if .NET 10 runtime is installed
bool IsDotNetRuntimeInstalled()
{
    // Run: dotnet --list-runtimes
    wchar_t cmdLine[] = L"dotnet --list-runtimes";

    SECURITY_ATTRIBUTES sa;
    sa.nLength = sizeof(SECURITY_ATTRIBUTES);
    sa.bInheritHandle = TRUE;
    sa.lpSecurityDescriptor = nullptr;

    HANDLE hReadPipe, hWritePipe;
    if (!CreatePipe(&hReadPipe, &hWritePipe, &sa, 0))
        return false;

    STARTUPINFOW si = { sizeof(STARTUPINFOW) };
    si.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    si.hStdOutput = hWritePipe;
    si.hStdError = hWritePipe;
    si.wShowWindow = SW_HIDE;

    PROCESS_INFORMATION pi;
    if (!CreateProcessW(nullptr, cmdLine, nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
    {
        CloseHandle(hReadPipe);
        CloseHandle(hWritePipe);
        return false;
    }

    CloseHandle(hWritePipe);

    // Read output
    char buffer[4096];
    DWORD bytesRead;
    std::string output;

    while (ReadFile(hReadPipe, buffer, sizeof(buffer) - 1, &bytesRead, nullptr) && bytesRead > 0)
    {
        buffer[bytesRead] = '\0';
        output += buffer;
    }

    CloseHandle(hReadPipe);
    WaitForSingleObject(pi.hProcess, INFINITE);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    // Check if output contains "Microsoft.WindowsDesktop.App 10.0"
    return output.find("Microsoft.WindowsDesktop.App 10.0") != std::string::npos;
}

// Download and install .NET 10 runtime
bool InstallDotNetRuntime()
{
    // Determine download URL based on architecture
    const wchar_t* downloadUrl;
#ifdef _WIN64
    downloadUrl = RUNTIME_URL_X64;
#else
    downloadUrl = RUNTIME_URL_X86;
#endif

    // Download to temp directory
    wchar_t tempPath[MAX_PATH];
    GetTempPathW(MAX_PATH, tempPath);
    std::wstring installerPath = std::wstring(tempPath) + L"dotnet-runtime-10-installer.exe";

    // Show progress message
    HWND hwndProgress = CreateWindowW(
        L"STATIC",
        L"Downloading .NET 10 Runtime...\n\nThis may take a few minutes.",
        WS_VISIBLE | WS_POPUP | SS_CENTER,
        (GetSystemMetrics(SM_CXSCREEN) - 400) / 2,
        (GetSystemMetrics(SM_CYSCREEN) - 100) / 2,
        400, 100,
        nullptr, nullptr, GetModuleHandle(nullptr), nullptr
    );

    // Download the installer
    HRESULT hr = URLDownloadToFileW(nullptr, downloadUrl, installerPath.c_str(), 0, nullptr);

    if (hwndProgress)
        DestroyWindow(hwndProgress);

    if (FAILED(hr))
        return false;

    // Run the installer with silent flags
    std::wstring cmdLine = installerPath + L" /install /quiet /norestart";

    STARTUPINFOW si = { sizeof(STARTUPINFOW) };
    si.wShowWindow = SW_SHOW;
    si.dwFlags = STARTF_USESHOWWINDOW;

    PROCESS_INFORMATION pi;
    if (!CreateProcessW(nullptr, const_cast<wchar_t*>(cmdLine.c_str()), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi))
    {
        DeleteFileW(installerPath.c_str());
        return false;
    }

    // Wait for installation to complete
    WaitForSingleObject(pi.hProcess, INFINITE);

    DWORD exitCode;
    GetExitCodeProcess(pi.hProcess, &exitCode);

    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    // Clean up installer
    DeleteFileW(installerPath.c_str());

    return exitCode == 0;
}

// Find dotnet.exe in PATH
std::wstring FindDotNetExe()
{
    // Try common locations
    wchar_t programFiles[MAX_PATH];

    // Try Program Files
    if (GetEnvironmentVariableW(L"ProgramFiles", programFiles, MAX_PATH) > 0)
    {
        std::wstring dotnetPath = std::wstring(programFiles) + L"\\dotnet\\dotnet.exe";
        if (PathFileExistsW(dotnetPath.c_str()))
            return dotnetPath;
    }

    // Try searching PATH
    wchar_t path[32768];
    if (GetEnvironmentVariableW(L"PATH", path, 32768) > 0)
    {
        wchar_t* context = nullptr;
        wchar_t* token = wcstok_s(path, L";", &context);

        while (token != nullptr)
        {
            std::wstring dotnetPath = std::wstring(token) + L"\\dotnet.exe";
            if (PathFileExistsW(dotnetPath.c_str()))
                return dotnetPath;

            token = wcstok_s(nullptr, L";", &context);
        }
    }

    // Fallback: just return "dotnet" and hope it's in PATH
    return L"dotnet";
}

// Load and run the .NET application DLL
int LoadAndRunDotNetApp(const wchar_t* dllPath, const wchar_t* appDirectory)
{
    // Run the exe directly (Costura.Fody has merged all DLLs into the exe)
    std::wstring cmdLine = L"\"" + std::wstring(dllPath) + L"\"";

    STARTUPINFOW si = { sizeof(STARTUPINFOW) };
    si.wShowWindow = SW_SHOW;
    si.dwFlags = STARTF_USESHOWWINDOW;

    PROCESS_INFORMATION pi;
    if (!CreateProcessW(
        nullptr,
        const_cast<wchar_t*>(cmdLine.c_str()),
        nullptr,
        nullptr,
        FALSE,
        0,
        nullptr,
        appDirectory,
        &si,
        &pi))
    {
        // Show error message
        DWORD error = GetLastError();
        wchar_t errorBuf[512];
        swprintf_s(errorBuf, L"Failed to launch application.\n\nCommand: %s\n\nError code: %d\n\nMake sure .NET 10 runtime is installed.",
            cmdLine.c_str(), error);
        MessageBoxW(nullptr, errorBuf, L"Launch Error", MB_OK | MB_ICONERROR);
        return 1;
    }

    // Don't wait - let it run in background
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    return 0;
}
