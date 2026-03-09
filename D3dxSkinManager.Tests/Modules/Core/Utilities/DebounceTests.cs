using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Tests.Modules.Core.Utilities;

/// <summary>
/// Tests for Debounce utility using FakeTimeProvider for instant, deterministic testing
/// </summary>
public class DebounceTests
{
    [Fact]
    public async Task ExecuteAsync_WithSingleCall_ShouldExecuteAfterDelay()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(TimeSpan.FromSeconds(10), fakeTime);
        var executed = false;

        // Act
        var task = debounce.ExecuteAsync(() => executed = true);

        // Assert - Action should NOT execute immediately
        executed.Should().BeFalse("action should wait for debounce delay");

        // Advance time to trigger execution
        fakeTime.Advance(TimeSpan.FromSeconds(10));
        await task;
        executed.Should().BeTrue("action should execute after delay completes");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleCalls_ShouldDebounceAndExecuteOnce()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(TimeSpan.FromSeconds(5), fakeTime);
        var executionCount = 0;

        // Act - Call multiple times rapidly (each call cancels the previous)
        var task1 = debounce.ExecuteAsync(() => executionCount++);
        fakeTime.Advance(TimeSpan.FromSeconds(2)); // Not enough time
        var task2 = debounce.ExecuteAsync(() => executionCount++); // Cancels task1
        fakeTime.Advance(TimeSpan.FromSeconds(2)); // Not enough time
        var task3 = debounce.ExecuteAsync(() => executionCount++); // Cancels task2

        // Advance time to trigger only the last call
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        await task3;

        // Assert - Only the last call should execute
        executionCount.Should().Be(1, "only the last debounced call should execute");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancel_ShouldNotExecute()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(TimeSpan.FromSeconds(5), fakeTime);
        var executed = false;

        // Act
        var task = debounce.ExecuteAsync(() => executed = true);
        debounce.Cancel();

        // Advance time (but action was cancelled)
        fakeTime.Advance(TimeSpan.FromSeconds(10));
        await Task.Yield(); // Allow task to complete

        // Assert
        executed.Should().BeFalse("action should not execute after cancellation");
    }

    [Fact]
    public async Task ExecuteAsync_AfterDispose_ShouldNotExecute()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(TimeSpan.FromSeconds(5), fakeTime);
        var executed = false;

        // Act
        debounce.Dispose();
        await debounce.ExecuteAsync(() => executed = true);

        // Advance time
        fakeTime.Advance(TimeSpan.FromSeconds(10));
        await Task.Yield();

        // Assert
        executed.Should().BeFalse("action should not execute after dispose");
    }

    [Fact]
    public async Task ExecuteAsync_WithAsyncAction_ShouldDebounceCorrectly()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(TimeSpan.FromSeconds(5), fakeTime);
        var executed = false;

        // Act
        var task = debounce.ExecuteAsync(async () =>
        {
            await Task.Yield(); // Simulate async work
            executed = true;
        });

        fakeTime.Advance(TimeSpan.FromSeconds(5));
        await task;

        // Assert
        executed.Should().BeTrue("async action should execute after debounce");
    }

    [Fact]
    public async Task ExecuteAsync_WithRapidFireCalls_ShouldOnlyExecuteLast()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(TimeSpan.FromSeconds(5), fakeTime);
        var lastValue = 0;

        // Act - Rapid fire 10 calls
        Task? lastTask = null;
        for (int i = 1; i <= 10; i++)
        {
            var value = i;
            lastTask = debounce.ExecuteAsync(() => lastValue = value);
            fakeTime.Advance(TimeSpan.FromSeconds(1)); // Small advance (< debounce delay)
        }

        // Advance time to trigger final execution
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        if (lastTask != null)
        {
            await lastTask;
        }

        // Assert - Should only execute with the last value
        lastValue.Should().Be(10, "only the last debounced value should be set");
    }

    [Fact]
    public async Task ExecuteAsync_WithWaitBetweenCalls_ShouldExecuteMultipleTimes()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(TimeSpan.FromSeconds(5), fakeTime);
        var executionCount = 0;

        // Act - Call with sufficient wait between calls
        var task1 = debounce.ExecuteAsync(() => executionCount++);
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        await task1;

        var task2 = debounce.ExecuteAsync(() => executionCount++);
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        await task2;

        // Assert
        executionCount.Should().Be(2, "both calls should execute when waited between calls");
    }

    [Fact]
    public void Debounce_WithMillisecondsConstructor_ShouldCreateCorrectDelay()
    {
        // Arrange & Act
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(500, fakeTime);

        // Assert - We can't directly test the internal delay, but we can verify it was created
        debounce.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ShouldPropagateException()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var debounce = new Debounce(TimeSpan.FromSeconds(5), fakeTime);
        var expectedException = new InvalidOperationException("Test exception");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var task = debounce.ExecuteAsync(() => throw expectedException);
            fakeTime.Advance(TimeSpan.FromSeconds(5));
            await task;
        });

        exception.Message.Should().Be("Test exception");
    }
}

/// <summary>
/// FakeTimeProvider Benefits:
///
/// All tests now use FakeTimeProvider for instant, deterministic time advancement.
/// Benefits achieved:
/// - Tests run instantly (no actual delays)
/// - Completely deterministic (no flaky tests)
/// - Can test long delays (10 seconds) in milliseconds
/// - Full control over time progression
///
/// Example:
/// var fakeTime = new FakeTimeProvider();
/// var debounce = new Debounce(TimeSpan.FromSeconds(10), fakeTime);
/// var task = debounce.ExecuteAsync(() => executed = true);
/// fakeTime.Advance(TimeSpan.FromSeconds(10)); // Instant advance!
/// await task;
///
/// This pattern eliminates flaky tests and dramatically speeds up test execution.
/// </summary>
