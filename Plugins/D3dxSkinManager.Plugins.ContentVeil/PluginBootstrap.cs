using System.Reflection;
using System.Runtime.CompilerServices;

namespace D3dxSkinManager.Plugins.ContentVeil;

/// <summary>
/// Single-dll plumbing: the managed Microsoft.ML.OnnxRuntime.dll is EMBEDDED in this assembly and
/// served through AssemblyResolve. The module initializer runs when the host loads the plugin
/// assembly — before any plugin type that references ONNX Runtime gets JITed, so the resolve hook
/// is always in place first.
/// </summary>
internal static class PluginBootstrap
{
    private static Assembly? _onnxRuntime;

    [ModuleInitializer]
    internal static void Init()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            if (!args.Name.StartsWith("Microsoft.ML.OnnxRuntime", StringComparison.OrdinalIgnoreCase))
                return null;
            if (_onnxRuntime != null) return _onnxRuntime;

            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("ContentVeilPlugin.Microsoft.ML.OnnxRuntime.dll");
            if (stream == null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _onnxRuntime = Assembly.Load(ms.ToArray());
            return _onnxRuntime;
        };
    }

    /// <summary>Extract the embedded NATIVE onnxruntime dlls into <paramref name="targetDir"/>
    /// (skipped when already present with the right size) and pre-load them so the managed
    /// wrapper's DllImports bind to these copies. Call BEFORE the first InferenceSession.
    /// THROWS a descriptive exception when a native is missing from the package or the OS cannot
    /// load it — the caller surfaces that so a broken install fails loudly, not with a cryptic
    /// per-image crash later.</summary>
    internal static void EnsureNativeLibraries(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var name in new[] { "onnxruntime.dll", "onnxruntime_providers_shared.dll" })
        {
            var path = Path.Combine(targetDir, name);
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream($"ContentVeilPlugin.native.{name}");
            if (stream == null)
                throw new InvalidOperationException(
                    $"the Content Veil AI package is missing an embedded native library ({name}) — reinstall or update the pack");
            if (!File.Exists(path) || new FileInfo(path).Length != stream.Length)
            {
                using var file = File.Create(path);
                stream.CopyTo(file);
            }
        }
        // Load order matters: providers_shared first (onnxruntime.dll links against it) — best-effort;
        // onnxruntime.dll loading is the decisive check.
        global::System.Runtime.InteropServices.NativeLibrary.TryLoad(
            Path.Combine(targetDir, "onnxruntime_providers_shared.dll"), out _);
        if (!global::System.Runtime.InteropServices.NativeLibrary.TryLoad(
                Path.Combine(targetDir, "onnxruntime.dll"), out _))
            throw new InvalidOperationException(
                "failed to load the ONNX Runtime native library (onnxruntime.dll) — the Microsoft Visual C++ 2019+ redistributable may be missing on this system");
    }
}
