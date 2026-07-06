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
    /// <summary>Per-provider login target: where to send the user, which origin owns the cookie,
    /// which cookie names prove a completed login (so we don't save an anonymous session), and an
    /// optional script to bring the login UI into view once the page renders.</summary>
    private sealed record LoginTarget(string LoginUrl, string CookieUrl, string[] AuthCookieNames,
        string DisplayName, string? FocusScript = null);

    private static readonly IReadOnlyDictionary<string, LoginTarget> Targets =
        new Dictionary<string, LoginTarget>(StringComparer.OrdinalIgnoreCase)
        {
            // Quark: the session cookies (__puus/__pus/__kps/__uid) live on the PARENT domain
            // `.quark.cn`, not `pan.quark.cn` (verified from the login profile's cookie DB). Read
            // them from the API origin (drive-pc.quark.cn) so the domain cookies — the exact set the
            // resolver must send to that host — are what we capture.
            // pan.quark.cn renders its LoginComponent (QR / phone / wx) only at DESKTOP width, docked
            // to the RIGHT of a big promo hero. So keep the window desktop-width to make it render,
            // then ISOLATE the login: hide every sibling up the ancestor chain (WITHOUT moving the
            // node — so React keeps reconciling it and the QR keeps polling) and pin the login to fill
            // the viewport, centered on white. Re-applied on an interval to survive SPA re-renders, so
            // the window reads as a clean Quark login box, not the marketing homepage.
            ["quark"] = new("https://pan.quark.cn/", "https://drive-pc.quark.cn",
                new[] { "__puus", "__pus", "__kps", "__uid" }, "夸克网盘",
                FocusScript: """
                (function(){
                  var signalled = false;
                  function isolate(){ try {
                    // The whole login box is "LoginComponent--modal--<hash>" (header + QR + footer +
                    // agreement). The trailing "--" excludes its sub-parts (--modal-header-- etc).
                    var el = document.querySelector('[class*="LoginComponent--modal--"]')
                          || document.querySelector('[class*="LoginComponent"]');
                    if (!el) return; // no LoginComponent (already logged in, or Quark changed the page)
                                     // → do nothing; the window falls back to showing the page as-is.
                    // Hide every sibling up the ancestor chain (don't MOVE the node — React keeps
                    // reconciling it and the QR keeps polling).
                    var node = el;
                    while (node && node !== document.body) {
                      var p = node.parentElement; if (!p) break;
                      for (var i = 0; i < p.children.length; i++) {
                        var c = p.children[i];
                        if (c !== node && c.tagName !== 'SCRIPT' && c.tagName !== 'STYLE')
                          c.style.setProperty('display','none','important');
                      }
                      node = p;
                    }
                    // Dock the login box LEFT, vertically centered. right:auto keeps its intrinsic
                    // (shrink-to-fit) width; left:0 anchors it; margin:auto centres it vertically.
                    // (Horizontal centering is unreliable — the --modal-- element isn't a clean fixed
                    // -width block, so left:50%/transform mis-place the card; and the window can't
                    // shrink to the box because Quark only renders the login above a ~1000px
                    // breakpoint, wider than the box — so some margin is unavoidable, kept on the
                    // right.) Don't touch display/flex (scatters its internal tabs). Clean white surround.
                    el.style.setProperty('position','fixed','important');
                    el.style.setProperty('top','0','important');
                    el.style.setProperty('bottom','0','important');
                    el.style.setProperty('left','0','important');
                    el.style.setProperty('right','auto','important');
                    el.style.setProperty('margin','auto','important');
                    el.style.setProperty('transform','none','important');
                    el.style.setProperty('z-index','2147483647','important');
                    document.documentElement.style.background = '#fff';
                    document.body.style.background = '#fff';
                    document.documentElement.style.overflow = 'hidden';
                    document.body.style.overflow = 'hidden';
                    if (!signalled && window.chrome && window.chrome.webview) {
                      var r = el.getBoundingClientRect();
                      if (r.width > 100 && r.height > 100) {
                        signalled = true;
                        window.chrome.webview.postMessage('login-ready'); // C# reveals the window on this
                      }
                    }
                  } catch(e){} }
                  var n = 0, iv = setInterval(function(){ isolate(); if (++n > 40) clearInterval(iv); }, 500);
                  isolate();
                })();
                """),
        };

    /// <summary>Shown IN the login window when the page fails to load — a retry button posts "reload".</summary>
    private const string LoginErrorHtml = """
        <!doctype html><html><head><meta charset="utf-8"><style>
        html,body{height:100%;margin:0;display:flex;align-items:center;justify-content:center;
          background:#fff;font-family:'Microsoft YaHei',system-ui,sans-serif;color:#333}
        .box{text-align:center}
        .t{font-size:16px;margin-bottom:6px}.s{font-size:13px;color:#999;margin-bottom:18px}
        button{font-size:14px;padding:8px 22px;border:none;border-radius:6px;background:#1677ff;
          color:#fff;cursor:pointer}button:hover{background:#4096ff}
        </style></head><body><div class="box">
        <div class="t">页面加载失败 / Page failed to load</div>
        <div class="s">请检查网络后重试 · Check your connection and retry</div>
        <button onclick="window.chrome.webview.postMessage('reload')">重新加载 / Reload</button>
        </div></body></html>
        """;

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
        // Desktop-WIDTH window: Quark only renders its login at desktop width (a narrow window reflows
        // to a mobile layout with no login). The isolate script then hides the marketing page and
        // centres the login box in this window. Clamped to the monitor's working area.
        // Desktop-WIDTH window: Quark only renders its login at desktop width (a narrower window
        // reflows to a mobile layout with no login), so 1024 is about the tightest that still renders
        // it. The isolate script hides the marketing page and docks the login box left; height fits
        // the box with room for the agreement line.
        var work = Screen.FromControl(mainForm).WorkingArea;
        var winW = Math.Min(680, work.Width - 40);
        var winH = Math.Min(800, work.Height - 60);

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
        // (the "already logged in" path closes before we ever reveal, so the window never shows).
        // The window stays HIDDEN until the login page is READY: the isolate script posts
        // "login-ready" once it has framed the login box → we reveal then (no homepage flash). The
        // grace-tick reveal below is only a slow FALLBACK if that message never arrives.
        const int graceTicks = 12; // ~9.6s fallback — the login-ready message normally reveals first
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

        // Once the login page renders, isolate its login panel (per-provider script) so the window
        // reads as a login box, not the site homepage. The window is loaded HIDDEN and only revealed
        // when the script posts "login-ready" (page framed) — no homepage/loading flash.
        webView.CoreWebView2.NavigationCompleted += async (_, e) =>
        {
            if (form.IsDisposed) return;
            if (!e.IsSuccess)
            {
                // Page failed to load → show the retry overlay and reveal so the user sees it.
                try { webView.CoreWebView2.NavigateToString(LoginErrorHtml); } catch { }
                RevealIfHidden();
                return;
            }
            if (!string.IsNullOrEmpty(target.FocusScript))
                try { await webView.CoreWebView2.ExecuteScriptAsync(target.FocusScript).ConfigureAwait(true); } catch { }
        };
        webView.CoreWebView2.WebMessageReceived += (_, e) =>
        {
            string? msg = null;
            try { msg = e.TryGetWebMessageAsString(); } catch { }
            // Reveal once the login box is framed (script posts "login-ready"); reload on the retry button.
            if (msg == "login-ready" && !captured) RevealIfHidden();
            else if (msg == "reload") { try { webView.CoreWebView2.Navigate(target.LoginUrl); } catch { } }
        };

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
