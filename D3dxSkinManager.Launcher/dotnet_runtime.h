// .NET Runtime detection, installation, and hosting
#pragma once

#include <windows.h>

// Check if .NET 10 runtime is installed
bool IsDotNetRuntimeInstalled();

// Download and install .NET 10 runtime
bool InstallDotNetRuntime();

// Load and run the .NET application DLL using .NET hosting API
int LoadAndRunDotNetApp(const wchar_t* dllPath, const wchar_t* appDirectory);
