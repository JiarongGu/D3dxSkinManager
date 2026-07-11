namespace D3dxSkinManager.Infrastructure;

/// <summary>
/// Resolves the install ROOT the app should treat as its base directory.
///
/// The launcher lives at the install root, but the runtime exe now lives in <c>{install}/lib</c>, so
/// <c>AppDomain.CurrentDomain.BaseDirectory</c> (the exe's own folder) resolves to <c>{install}/lib</c>
/// and every install-relative path (<c>data/</c>, <c>res/</c>, <c>libs/</c>, <c>.update/</c>) would be
/// wrong. The launcher passes the true install root via <c>--app-root "&lt;path&gt;"</c>; this helper
/// reads it, falling back to <paramref name="fallback"/> when the flag is absent (e.g. a dev run or a
/// direct double-click of the lib exe).
/// </summary>
public static class AppRootArg
{
    public const string Flag = "--app-root";

    /// <summary>
    /// Returns the value passed with <c>--app-root</c> (space-separated <c>--app-root &lt;path&gt;</c> or
    /// joined <c>--app-root=&lt;path&gt;</c>, surrounding quotes/whitespace stripped), or
    /// <paramref name="fallback"/> when the flag is missing or its value is blank.
    /// </summary>
    public static string Resolve(string[]? args, string fallback)
    {
        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == null) continue;

                // --app-root <path>
                if (string.Equals(arg, Flag, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        var val = Clean(args[i + 1]);
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                    break;
                }

                // --app-root=<path>
                if (arg.StartsWith(Flag + "=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = Clean(arg.Substring(Flag.Length + 1));
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                    break;
                }
            }
        }
        return fallback;
    }

    private static string Clean(string? s) => s?.Trim().Trim('"').Trim() ?? string.Empty;
}
