// Auto-update functionality
#pragma once

#include <windows.h>
#include <string>

// Check for, and (with the user's consent) apply an application update before the main app launches.
// Strategy: download the latest release manifest.json from GitHub (via the stable
// releases/latest/download redirect), compare its version to the locally-installed manifest, and if
// newer, download the release zip, extract it, overlay every file EXCEPT the launcher itself, delete
// files that the new manifest no longer lists, and refresh the local manifest.
//
// Returns true if the check completed (whether or not an update was applied); false on a hard failure.
// Non-fatal: main.cpp continues to launch the app regardless.
bool CheckForUpdates(const std::wstring& appDirectory);
