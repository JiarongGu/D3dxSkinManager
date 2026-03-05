// Auto-update functionality
#pragma once

#include <windows.h>
#include <string>

// Check for application updates
// Returns true if check completed (whether update found or not)
// Returns false if check failed
bool CheckForUpdates(const std::wstring& appDirectory);

// Download and apply an update
bool DownloadAndApplyUpdate(const std::wstring& updateUrl, const std::wstring& appDirectory);
