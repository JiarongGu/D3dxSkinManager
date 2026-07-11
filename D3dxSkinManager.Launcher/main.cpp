// D3dxSkinManager Launcher
// Native C++ bootstrapper that:
// 1. Applies a pending app update the app downloaded + staged (before the app starts)
// 2. Checks for .NET 10 runtime and installs if missing
// 3. Loads the main application

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>
#include <string>
#include "dotnet_runtime.h"
#include "updater.h"

#pragma comment(lib, "shlwapi.lib")

constexpr auto APP_NAME = L"D3dxSkinManager";
// The runtime lives in lib/ now; the launcher IS the top-level D3dxSkinManager.exe. The launcher passes
// its own directory (the install root) to the app via --app-root so the app resolves data/, res/, libs/
// and .update/ against the install root, not lib/ (see dotnet_runtime.cpp / AppRootArg.cs).
constexpr auto MAIN_EXE = L"lib\\D3dxSkinManager.App.exe";

// Get the directory where the launcher executable is located
std::wstring GetLauncherDirectory()
{
    wchar_t path[MAX_PATH];
    GetModuleFileNameW(nullptr, path, MAX_PATH);
    PathRemoveFileSpecW(path);
    return std::wstring(path);
}

// Entry point
int WINAPI wWinMain(
    _In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPWSTR lpCmdLine,
    _In_ int nShowCmd)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(nShowCmd);

    // Get launcher directory
    std::wstring launcherDir = GetLauncherDirectory();
    std::wstring mainExePath = launcherDir + L"\\" + MAIN_EXE;

    // Test/diagnostic seam: apply any staged update against this directory and exit WITHOUT launching
    // the app (no .NET check, no MessageBox). Lets a harness exercise the real apply on a sandbox
    // install dir (devtools/scripts/test-update-apply.mjs). Not used in normal launches.
    if (lpCmdLine != nullptr && wcsstr(lpCmdLine, L"--apply-and-exit") != nullptr)
    {
        ApplyPendingUpdate(launcherDir);
        return 0;
    }

    // Step 1: Apply a pending update the app already downloaded + staged (before loading the main app).
    // No-op if nothing is staged; the launcher never checks GitHub itself (the app does that).
    ApplyPendingUpdate(launcherDir);

    // Step 2: Verify .NET 10 runtime is installed
    if (!IsDotNetRuntimeInstalled())
    {
        int result = MessageBoxW(
            nullptr,
            L"D3dxSkinManager requires .NET 10 Desktop Runtime to run.\n\n"
            L"Would you like to download and install it now?\n\n"
            L"This will download approximately 50MB and may take a few minutes.",
            L"D3dxSkinManager - Runtime Required",
            MB_YESNO | MB_ICONINFORMATION
        );

        if (result != IDYES)
        {
            MessageBoxW(
                nullptr,
                L"Application cannot start without .NET 10 Runtime.\n\n"
                L"You can download it manually from:\n"
                L"https://dotnet.microsoft.com/download/dotnet/10.0",
                L"D3dxSkinManager - Installation Cancelled",
                MB_OK | MB_ICONWARNING
            );
            return 1;
        }

        // Install the runtime
        if (!InstallDotNetRuntime())
        {
            MessageBoxW(
                nullptr,
                L"Failed to install .NET 10 Runtime.\n\n"
                L"Please download and install it manually from:\n"
                L"https://dotnet.microsoft.com/download/dotnet/10.0",
                L"D3dxSkinManager - Installation Failed",
                MB_OK | MB_ICONERROR
            );
            return 1;
        }

        // Verify installation
        if (!IsDotNetRuntimeInstalled())
        {
            MessageBoxW(
                nullptr,
                L"Runtime installation completed but verification failed.\n\n"
                L"Please restart your computer and try again.",
                L"D3dxSkinManager - Verification Failed",
                MB_OK | MB_ICONWARNING
            );
            return 1;
        }

        MessageBoxW(
            nullptr,
            L".NET 10 Runtime installed successfully!\n\n"
            L"D3dxSkinManager will now start.",
            L"D3dxSkinManager - Installation Complete",
            MB_OK | MB_ICONINFORMATION
        );
    }

    // Step 3: Load and run the main application
    if (!PathFileExistsW(mainExePath.c_str()))
    {
        std::wstring errorMsg = L"Main application not found:\n" + mainExePath + L"\n\n"
            L"Please reinstall the application.";
        MessageBoxW(
            nullptr,
            errorMsg.c_str(),
            L"D3dxSkinManager - Error",
            MB_OK | MB_ICONERROR
        );
        return 1;
    }

    // Load the .NET runtime and execute the main application
    int exitCode = LoadAndRunDotNetApp(mainExePath.c_str(), launcherDir.c_str());

    return exitCode;
}
