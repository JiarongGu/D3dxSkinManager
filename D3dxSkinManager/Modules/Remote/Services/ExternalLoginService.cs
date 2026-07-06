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
    /// <summary>Show the login window for a provider; resolves when the user closes it, with the
    /// resulting (cookie-free) account info. Throws EXTERNAL_LOGIN_UNAVAILABLE off a known provider.</summary>
    Task<OnlineStorageAccountInfo> LoginAsync(string provider, CancellationToken ct = default);
}

public class ExternalLoginService : IExternalLoginService
{
    /// <summary>Per-provider login target: where to send the user, which origin owns the cookie, and
    /// which cookie names prove a completed login (so we don't save an anonymous session).</summary>
    private sealed record LoginTarget(string LoginUrl, string CookieUrl, string[] AuthCookieNames, string DisplayName);

    private static readonly IReadOnlyDictionary<string, LoginTarget> Targets =
        new Dictionary<string, LoginTarget>(StringComparer.OrdinalIgnoreCase)
        {
            // Quark: __puus/__pus/kps are the session cookies the drive API checks.
            ["quark"] = new("https://pan.quark.cn/", "https://pan.quark.cn",
                new[] { "__puus", "__pus", "kps" }, "夸克网盘"),
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

    private async Task ShowLoginWindowAsync(string provider, LoginTarget target, Form mainForm,
        TaskCompletionSource<OnlineStorageAccountInfo> tcs)
    {
        var form = new Form
        {
            Text = $"{target.DisplayName} — Login",
            Width = 960,
            Height = 720,
            StartPosition = FormStartPosition.CenterParent,
            Owner = mainForm,
            BackColor = Color.FromArgb(26, 26, 26),
        };
        var webView = new WebView2 { Dock = DockStyle.Fill, BackColor = Color.FromArgb(26, 26, 26) };
        form.Controls.Add(webView);

        // Persistent, per-provider WebView2 profile so the login sticks between sessions.
        var userDataFolder = Path.Combine(_globalPaths.GlobalSettingsDirectory, "webview-login", provider);
        Directory.CreateDirectory(userDataFolder);
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder).ConfigureAwait(true);
        await webView.EnsureCoreWebView2Async(env).ConfigureAwait(true);

        var captured = false;
        // Capture on close (user decides when login is done). Idempotent via `captured`.
        form.FormClosing += async (_, _) =>
        {
            if (captured) return;
            captured = true;
            try
            {
                var info = await CaptureAsync(provider, target, webView).ConfigureAwait(true);
                tcs.TrySetResult(info);
            }
            catch (Exception ex)
            {
                _logger.Error($"[ExternalLogin] capture failed: {ex.Message}", "ExternalLoginService", ex);
                tcs.TrySetResult(new OnlineStorageAccountInfo { Provider = provider, LoggedIn = false });
            }
        };

        webView.CoreWebView2.Navigate(target.LoginUrl);
        form.Show(mainForm);
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
