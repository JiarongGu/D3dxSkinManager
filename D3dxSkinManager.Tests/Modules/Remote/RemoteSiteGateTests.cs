using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// The WordPress password-gate unlock flow (RemoteSiteGate): logs in ONCE per source (GET login page to
/// seed the cookie, then POST the password), caches the unlocked state, re-logs-in after Invalidate, and
/// throws REMOTE_GATE_FAILED when the login is rejected. No network — a fake IDownloadService.
/// </summary>
public class RemoteSiteGateTests
{
    private static ILogHelper Log() => Mock.Of<ILogHelper>();

    private sealed class FakeDownload : IDownloadService
    {
        public int Gets;
        public int Posts;
        public IReadOnlyDictionary<string, string>? LastForm;
        public string PostResult = "<html><body>welcome, unlocked site</body></html>";

        public Task<string> GetStringAsync(string url, IReadOnlyDictionary<string, string>? headers = null, CancellationToken ct = default)
        { Gets++; return Task.FromResult("<html>login form</html>"); }

        public Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, IReadOnlyDictionary<string, string>? headers = null, CancellationToken ct = default)
        { Posts++; LastForm = form; return Task.FromResult(PostResult); }

        public Task<string> PostJsonAsync(string url, string jsonBody, IReadOnlyDictionary<string, string>? headers = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public string ManagedDirectory => throw new NotImplementedException();
        public Task<DownloadResult> DownloadToManagedAsync(string url, string fileName, IProgress<DownloadProgress>? progress = null, string? expectedSha256 = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public IReadOnlyList<ManagedDownloadInfo> ListManaged() => throw new NotImplementedException();
        public DownloadCleanupResult CleanupManaged(TimeSpan? olderThan = null) => throw new NotImplementedException();
    }

    private static RemoteSourceConfig Gated() => new()
    {
        Id = "kekehxl",
        Name = "可可站",
        BaseUrl = "https://kekehxl.top",
        Gate = new RemoteGateConfig { Type = "wordpress-password-protected", LoginPath = "/", PasswordField = "password_protected_pwd", Password = "kekehxl" },
    };

    [Theory]
    [InlineData("<html>password_protected_pwd field</html>", true)]
    [InlineData("<a href='/?password-protected=login'>x</a>", true)]
    [InlineData("<html>welcome, unlocked</html>", false)]
    [InlineData("", false)]
    public void IsGatePage_DetectsTheLoginForm(string body, bool expected)
        => RemoteSiteGate.IsGatePage(body).Should().Be(expected);

    [Fact]
    public async Task EnsureAuthenticated_NoGate_IsANoOp()
    {
        var dl = new FakeDownload();
        var gate = new RemoteSiteGate(dl, Log());
        await gate.EnsureAuthenticatedAsync(new RemoteSourceConfig { Id = "x", BaseUrl = "https://x" }, default);
        dl.Gets.Should().Be(0);
        dl.Posts.Should().Be(0);
    }

    [Fact]
    public async Task EnsureAuthenticated_LogsInOnce_ThenCaches()
    {
        var dl = new FakeDownload();
        var gate = new RemoteSiteGate(dl, Log());
        var cfg = Gated();

        await gate.EnsureAuthenticatedAsync(cfg, default);
        await gate.EnsureAuthenticatedAsync(cfg, default); // cached — no second login

        dl.Gets.Should().Be(1);
        dl.Posts.Should().Be(1);
        dl.LastForm!["password_protected_pwd"].Should().Be("kekehxl");
        dl.LastForm!["password-protected"].Should().Be("login");
    }

    [Fact]
    public async Task Invalidate_ForcesReLogin()
    {
        var dl = new FakeDownload();
        var gate = new RemoteSiteGate(dl, Log());
        var cfg = Gated();

        await gate.EnsureAuthenticatedAsync(cfg, default);
        gate.Invalidate(cfg.Id);
        await gate.EnsureAuthenticatedAsync(cfg, default);

        dl.Posts.Should().Be(2);
    }

    [Fact]
    public async Task WrongPassword_ThrowsGateFailed()
    {
        var dl = new FakeDownload { PostResult = "<html>please enter password_protected_pwd</html>" };
        var gate = new RemoteSiteGate(dl, Log());

        var act = () => gate.EnsureAuthenticatedAsync(Gated(), default);
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("REMOTE_GATE_FAILED");
    }
}
