using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Opens an in-app WebView2 window pointed at a download host's login page, lets the user sign in
/// with their normal credentials (never typed into our UI — it's the host's own page), then captures
/// the session cookie from that WebView2 profile and saves it to <see cref="IOnlineAccountStore"/>.
/// This is how a host whose downloads need auth (Quark) works "without its client": one browser login,
/// no cookie hand-copying. The WebView2 uses a PERSISTENT per-provider user-data folder so the login
/// survives re-opening the window (re-login refreshes an expired cookie).
/// </summary>
public interface IExternalLoginService
{
    /// <summary>Show the login window for a provider; resolves when login is detected (or the user
    /// closes it), with the resulting (cookie-free) account info. Throws EXTERNAL_LOGIN_UNAVAILABLE
    /// off a known provider.</summary>
    Task<OnlineStorageAccountInfo> LoginAsync(string provider, CancellationToken ct = default);

    /// <summary>Wipe the provider's persistent WebView2 login profile (its cached cookies/session), so
    /// a later login starts fresh instead of silently auto-logging-in. Called on logout. Best-effort.</summary>
    void ClearProfile(string provider);
}

public class ExternalLoginService : IExternalLoginService
{
    /// <summary>Per-provider login target: where to send the user, which origin owns the cookie, and
    /// which cookie names prove a completed login (so we don't save an anonymous session).</summary>
    private sealed record LoginTarget(string LoginUrl, string CookieUrl, string[] AuthCookieNames, string DisplayName);

    private static readonly IReadOnlyDictionary<string, LoginTarget> Targets =
        new Dictionary<string, LoginTarget>(StringComparer.OrdinalIgnoreCase)
        {
            // Quark: the session cookies (__puus/__pus/__kps/__uid) live on the PARENT domain
            // `.quark.cn`, not `pan.quark.cn` (verified from the login profile's cookie DB). Read
            // them from the API origin (drive-pc.quark.cn) so the domain cookies — the exact set the
            // resolver must send to that host — are what we capture.
            ["quark"] = new("https://pan.quark.cn/", "https://drive-pc.quark.cn",
                new[] { "__puus", "__pus", "__kps", "__uid" }, "夸克网盘"),
        };

    private readonly IFormInteractionService _forms;
    private readonly IOnlineAccountStore _accounts;
    private readonly IGlobalPathService _globalPaths;
    private readonly ILogHelper _logger;

    public ExternalLoginService(
        IFormInteractionService forms,
        IOnlineAccountStore accounts,
        IGlobalPathService globalPaths,
        ILogHelper logger)
    {
        _forms = forms;
        _accounts = accounts;
        _globalPaths = globalPaths;
        _logger = logger;
    }

    public Task<OnlineStorageAccountInfo> LoginAsync(string provider, CancellationToken ct = default)
    {
        if (!Targets.TryGetValue(provider, out var target))
            throw new OperationException("EXTERNAL_LOGIN_UNAVAILABLE", "provider", provider);

        var mainForm = _forms.GetMainForm()
            ?? throw new OperationException("EXTERNAL_LOGIN_UNAVAILABLE", "provider", provider);

        var tcs = new TaskCompletionSource<OnlineStorageAccountInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

        // All WinForms/WebView2 work marshals to the UI thread.
        mainForm.BeginInvoke(new Action(async () =>
        {
            try { await ShowLoginWindowAsync(provider, target, mainForm, tcs).ConfigureAwait(true); }
            catch (Exception ex)
            {
                _logger.Error($"[ExternalLogin] {provider} window failed: {ex.Message}", "ExternalLoginService", ex);
                tcs.TrySetException(new OperationException("EXTERNAL_LOGIN_FAILED", "provider", provider));
            }
        }));

        return tcs.Task;
    }

    public void ClearProfile(string provider)
    {
        // The WebView2 keeps its own cookie store in the per-provider user-data folder; removing our
        // saved cookie alone would still let the next login window silently auto-log-in from it.
        // Delete the folder so logout is a real logout. Best-effort — a locked folder (window still
        // open) just isn't cleared; the app-wide account cookie is already gone.
        var folder = ProfileFolder(provider);
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.Warn($"[ExternalLogin] could not clear {provider} profile: {ex.Message}", "ExternalLoginService");
        }
    }

    private string ProfileFolder(string provider) =>
        Path.Combine(_globalPaths.GlobalSettingsDirectory, "webview-login", provider);

    private async Task ShowLoginWindowAsync(string provider, LoginTarget target, Form mainForm,
        TaskCompletionSource<OnlineStorageAccountInfo> tcs)
    {
        // A REGULAR browser-size window so the user sees the login in Quark's normal page layout
        // (rather than a cramped panel). Clamped to fit the monitor's working area.
        var work = Screen.FromControl(mainForm).WorkingArea;
        var winW = Math.Min(1280, work.Width - 80);
        var winH = Math.Min(880, work.Height - 80);

        // Start OFF-SCREEN + off the taskbar: a WebView2 needs a real window handle to run, but if
        // the persistent profile is already logged in we capture the cookie and close WITHOUT the user
        // ever seeing a window (silent refresh — "no interaction => no window"). We only REVEAL it if
        // login is actually required (no session cookie within the grace period).
        var form = new Form
        {
            Text = $"{target.DisplayName} — Login",
            Width = winW,
            Height = winH,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ShowInTaskbar = false,
            Owner = mainForm,
            BackColor = Color.FromArgb(26, 26, 26),
        };
        var webView = new WebView2 { Dock = DockStyle.Fill, BackColor = Color.FromArgb(26, 26, 26) };
        form.Controls.Add(webView);

        void RevealIfHidden()
        {
            if (form.IsDisposed || form.ShowInTaskbar) return; // already visible
            form.ShowInTaskbar = true;
            form.StartPosition = FormStartPosition.CenterParent;
            var wa = Screen.FromControl(mainForm).WorkingArea;
            form.Location = new Point(wa.X + (wa.Width - form.Width) / 2, wa.Y + (wa.Height - form.Height) / 2);
            form.Activate();
            form.BringToFront();
            webView.Focus(); // keyboard/scan goes to the login page immediately
        }

        // Persistent, per-provider WebView2 profile so the login sticks between sessions (cleared on logout).
        var userDataFolder = ProfileFolder(provider);
        Directory.CreateDirectory(userDataFolder);
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder).ConfigureAwait(true);
        await webView.EnsureCoreWebView2Async(env).ConfigureAwait(true);

        var captured = false;

        // Shared finish: capture cookies once. `autoClose` = detected mid-session (close the window
        // for the user); manual close routes here too. Idempotent via `captured`.
        async Task FinishAsync(bool autoClose)
        {
            if (captured) return;
            captured = true;
            try
            {
                var info = await CaptureAsync(provider, target, webView).ConfigureAwait(true);
                tcs.TrySetResult(info);
                if (autoClose && !form.IsDisposed) form.Close();
            }
            catch (Exception ex)
            {
                _logger.Error($"[ExternalLogin] capture failed: {ex.Message}", "ExternalLoginService", ex);
                tcs.TrySetResult(new OnlineStorageAccountInfo { Provider = provider, LoggedIn = false });
            }
        }

        // DETECT login: poll the profile's cookies. Session cookie present => capture + auto-close
        // (the "already logged in" path closes before the grace elapses, so the window never shows).
        // Not present after the grace => reveal the window so the user can actually log in.
        const int graceTicks = 3; // ~2.4s at 800ms — enough for a persisted session to surface
        var poll = new global::System.Windows.Forms.Timer { Interval = 800 };
        var ticks = 0;
        poll.Tick += async (_, _) =>
        {
            if (captured || form.IsDisposed) { poll.Stop(); return; }
            ticks++;
            try
            {
                var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(target.CookieUrl).ConfigureAwait(true);
                if (cookies.Any(c => target.AuthCookieNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase)))
                {
                    poll.Stop();
                    await FinishAsync(autoClose: true).ConfigureAwait(true);
                    return;
                }
            }
            catch { /* transient — try again next tick */ }
            if (ticks >= graceTicks) RevealIfHidden(); // login needed → show the window
        };

        // Manual close (user gave up before logging in, or closed after) still captures.
        form.FormClosing += async (_, _) =>
        {
            poll.Stop();
            await FinishAsync(autoClose: false).ConfigureAwait(true);
        };
        form.FormClosed += (_, _) => poll.Dispose();

        webView.CoreWebView2.Navigate(target.LoginUrl);
        form.Show(mainForm); // shown off-screen; RevealIfHidden() brings it on-screen only if needed
        poll.Start();
    }

    /// <summary>Read the WebView2 profile's cookies for the host origin; save them iff a session
    /// cookie is present (so closing the window before logging in doesn't store an anonymous blob).</summary>
    private async Task<OnlineStorageAccountInfo> CaptureAsync(string provider, LoginTarget target, WebView2 webView)
    {
        var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(target.CookieUrl).ConfigureAwait(true);
        var hasAuth = cookies.Any(c => target.AuthCookieNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase));
        if (!hasAuth)
        {
            _logger.Info($"[ExternalLogin] {provider}: no session cookie — not saving (login not completed)", "ExternalLoginService");
            return new OnlineStorageAccountInfo { Provider = provider, LoggedIn = false };
        }

        var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
        _accounts.Save(new OnlineStorageAccount
        {
            Provider = provider,
            DisplayName = target.DisplayName,
            Cookie = cookieHeader,
        });
        _logger.Info($"[ExternalLogin] {provider}: captured {cookies.Count} cookies, saved account", "ExternalLoginService");
        return new OnlineStorageAccountInfo
        {
            Provider = provider,
            DisplayName = target.DisplayName,
            LoggedIn = true,
            SavedAtUtc = DateTime.UtcNow,
        };
    }
}
