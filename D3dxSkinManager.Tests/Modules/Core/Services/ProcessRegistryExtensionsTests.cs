using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core.Services;

/// <summary>
/// Tests the RunTrackedAsync fire-and-forget wrapper: Start + background run, then Complete on success /
/// Cancel on cancellation / Fail (+ onError) on any other exception. It returns immediately, so each test
/// awaits a TaskCompletionSource the mock signals from the relevant lifecycle call.
/// </summary>
public class ProcessRegistryExtensionsTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);

    private static (Mock<IProcessRegistry> reg, TaskCompletionSource done) NewRegistry()
    {
        var reg = new Mock<IProcessRegistry>();
        reg.Setup(r => r.Start(It.IsAny<ProcessType>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("id1");
        reg.Setup(r => r.GetToken("id1")).Returns(CancellationToken.None);
        return (reg, new TaskCompletionSource());
    }

    [Fact]
    public async Task RunTrackedAsync_Completes_OnSuccess()
    {
        var (reg, done) = NewRegistry();
        reg.Setup(r => r.Complete("id1")).Callback(() => done.TrySetResult());

        reg.Object.RunTrackedAsync(ProcessType.Download, "t", (_, _) => Task.CompletedTask);

        await done.Task.WaitAsync(Wait);
        reg.Verify(r => r.Complete("id1"), Times.Once);
        reg.Verify(r => r.Fail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        reg.Verify(r => r.Cancel(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunTrackedAsync_Fails_AndCallsOnError_OnException()
    {
        var (reg, done) = NewRegistry();
        reg.Setup(r => r.Fail("id1", "boom")).Callback(() => done.TrySetResult());
        Exception? seen = null;

        reg.Object.RunTrackedAsync(ProcessType.Download, "t",
            (_, _) => throw new InvalidOperationException("boom"),
            onError: ex => seen = ex);

        await done.Task.WaitAsync(Wait);
        reg.Verify(r => r.Fail("id1", "boom"), Times.Once);
        reg.Verify(r => r.Complete(It.IsAny<string>()), Times.Never);
        seen.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task RunTrackedAsync_Cancels_OnOperationCanceled()
    {
        var (reg, done) = NewRegistry();
        reg.Setup(r => r.Cancel("id1")).Callback(() => done.TrySetResult());

        reg.Object.RunTrackedAsync(ProcessType.Download, "t",
            (_, _) => throw new OperationCanceledException());

        await done.Task.WaitAsync(Wait);
        reg.Verify(r => r.Cancel("id1"), Times.Once);
        reg.Verify(r => r.Fail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        reg.Verify(r => r.Complete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunTrackedAsync_StartsWithTheGivenMetadata()
    {
        var (reg, done) = NewRegistry();
        reg.Setup(r => r.Complete("id1")).Callback(() => done.TrySetResult());

        reg.Object.RunTrackedAsync(ProcessType.Download, "the title", (_, _) => Task.CompletedTask,
            cancellable: true, titleKey: "process.x", titleArg: "arg");

        await done.Task.WaitAsync(Wait);
        reg.Verify(r => r.Start(ProcessType.Download, "the title", true,
            It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>(), "process.x", "arg"), Times.Once);
    }
}
