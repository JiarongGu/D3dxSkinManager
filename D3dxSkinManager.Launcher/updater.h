// Auto-update -- APPLY phase
#pragma once

#include <windows.h>
#include <string>

// Apply a pending update that the app already downloaded + staged under {appDir}/.update/.
// Runs before the app launches (a running exe can't replace itself). If nothing is staged this is a
// cheap no-op -- the launcher never checks GitHub or prompts; that is the app's job (UpdateService).
// Returns true when done (whether or not an update was applied); never fatal.
bool ApplyPendingUpdate(const std::wstring& appDirectory);
