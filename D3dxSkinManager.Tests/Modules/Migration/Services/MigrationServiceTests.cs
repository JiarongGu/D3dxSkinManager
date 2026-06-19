using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Services;
using D3dxSkinManager.Modules.Migration.Steps;

namespace D3dxSkinManager.Tests.Modules.Migration.Services;

/// <summary>
/// Tests for MigrationService orchestration after the ctor was refactored to inject
/// IEnumerable&lt;IMigrationStep&gt;: steps run in StepNumber order regardless of injection order,
/// progress is reported, a failing step stops the run + records which step failed, a pre-cancelled
/// token runs nothing, and AnalyzeSourceAsync drives step 1. Steps are fakes — no real Python data.
/// </summary>
public class MigrationServiceTests
{
    private static MigrationService Service(params IMigrationStep[] steps)
        => new(Mock.Of<IProfilePathService>(), Mock.Of<ILogHelper>(), steps);

    private static MigrationOptions Options() => new() { SourcePath = "src" };

    [Fact]
    public async Task MigrateAsync_RunsStepsInStepNumberOrder_RegardlessOfInjectionOrder()
    {
        var order = new List<int>();
        var service = Service(
            new FakeStep(3, "C", _ => order.Add(3)),
            new FakeStep(1, "A", _ => order.Add(1)),
            new FakeStep(2, "B", _ => order.Add(2)));

        var result = await service.MigrateAsync(Options());

        result.Success.Should().BeTrue();
        order.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MigrateAsync_StepThrows_StopsAndRecordsFailedStep()
    {
        var executed = new List<int>();
        var service = Service(
            new FakeStep(1, "A", _ => executed.Add(1)),
            new FakeStep(2, "B", _ => throw new InvalidOperationException("boom")),
            new FakeStep(3, "C", _ => executed.Add(3)));

        var result = await service.MigrateAsync(Options());

        result.Success.Should().BeFalse();
        result.FailedAtStep.Should().Be(2);
        result.FailedStepName.Should().Be("B");
        result.Errors.Should().Contain(e => e.Contains("boom"));
        executed.Should().Equal(1); // step 3 never ran
    }

    [Fact]
    public async Task MigrateAsync_ReportsProgress_ThroughCompletion()
    {
        var progress = new SyncProgress<MigrationProgress>();
        var service = Service(new FakeStep(1, "Only", _ => { }));

        await service.MigrateAsync(Options(), progress);

        progress.Items.Should().NotBeEmpty();
        progress.Items.Should().Contain(p => p.Stage == MigrationStage.Complete && p.PercentComplete == 100);
    }

    [Fact]
    public async Task MigrateAsync_PreCancelledToken_RunsNothing()
    {
        var executed = new List<int>();
        var service = Service(new FakeStep(1, "A", _ => executed.Add(1)));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.MigrateAsync(Options(), null, cts.Token);

        result.Success.Should().BeFalse();
        executed.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeSourceAsync_DrivesStep1_AndReturnsItsAnalysis()
    {
        var analysis = new MigrationAnalysis { IsValid = true };
        var service = Service(
            new FakeStep(1, "Analyze", ctx => ctx.Analysis = analysis),
            new FakeStep(2, "B", _ => { }));

        var result = await service.AnalyzeSourceAsync("python-path");

        result.Should().BeSameAs(analysis);
    }

    // ---- fakes --------------------------------------------------------------

    private sealed class FakeStep : IMigrationStep
    {
        private readonly Action<MigrationContext> _onExecute;
        public FakeStep(int number, string name, Action<MigrationContext> onExecute)
        {
            StepNumber = number;
            StepName = name;
            _onExecute = onExecute;
        }
        public int StepNumber { get; }
        public string StepName { get; }
        public Task ExecuteAsync(MigrationContext context, IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _onExecute(context);
            return Task.CompletedTask;
        }
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        public List<T> Items { get; } = new();
        public void Report(T value) => Items.Add(value);
    }
}
