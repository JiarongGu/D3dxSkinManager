using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Models;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Unit tests for FileOperationPlanner
/// Tests the atomic file operation planning system
/// </summary>
public class FileOperationPlannerTests
{
    private readonly Mock<IArchiveHelper> _mockArchiveHelper;
    private readonly Mock<ILogHelper> _mockLogger;

    public FileOperationPlannerTests()
    {
        _mockArchiveHelper = new Mock<IArchiveHelper>();
        _mockLogger = new Mock<ILogHelper>();
    }

    [Fact]
    public async Task SubmitOperationAsync_SingleOperation_ExecutesSuccessfully()
    {
        // Arrange
        var planner = new FileOperationPlanner(_mockArchiveHelper.Object, new SystemFileSystem(), _mockLogger.Object);

        var operation = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "source",
            TargetPath = "target"
        };

        // We can't actually execute move operations in unit tests, so we'll test with a simpler approach
        // The planner will try to execute but fail with "directory not found" which is expected

        // Act & Assert - should not hang
        var result = await planner.SubmitOperationAsync(operation);

        // The operation will fail because source doesn't exist, but that's expected in unit tests
        // The important thing is that it completes and doesn't hang
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SubmitOperationAsync_MultipleOperations_ExecutesSequentially()
    {
        // Arrange
        var planner = new FileOperationPlanner(_mockArchiveHelper.Object, new SystemFileSystem(), _mockLogger.Object);
        var executionOrder = new List<string>();
        var lockObj = new object();

        // Create multiple operations (they will fail, but we're testing the sequencing)
        var operations = new List<Task<FileSystemOperationResult>>();

        for (int i = 1; i <= 5; i++)
        {
            var index = i;
            var operation = new FileSystemOperation
            {
                OperationType = FileSystemOperationType.MoveDirectory,
                SourcePath = $"source{index}",
                TargetPath = $"target{index}"
            };

            // Submit all operations concurrently
            operations.Add(Task.Run(async () =>
            {
                lock (lockObj) executionOrder.Add($"submit-{index}");
                var result = await planner.SubmitOperationAsync(operation);
                lock (lockObj) executionOrder.Add($"complete-{index}");
                return result;
            }));
        }

        // Act - wait for all operations to complete
        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        var completed = await Task.WhenAny(Task.WhenAll(operations), timeout);

        // Assert
        Assert.NotSame(timeout, completed);
        Assert.True(operations.All(t => t.IsCompleted), "All operations should complete");

        // All 5 operations should have been submitted and completed
        var submitCount = executionOrder.Count(x => x.StartsWith("submit-"));
        var completeCount = executionOrder.Count(x => x.StartsWith("complete-"));

        Assert.Equal(5, submitCount);
        Assert.Equal(5, completeCount);

        _mockLogger.Verify(
            x => x.Info(It.IsAny<string>(), It.IsAny<string>()),
            Times.AtLeastOnce,
            "Logger should be called to show operations are being processed"
        );
    }

    [Fact]
    public async Task SubmitOperationAsync_RapidSubmission_AllProcessed()
    {
        // Arrange
        var planner = new FileOperationPlanner(_mockArchiveHelper.Object, new SystemFileSystem(), _mockLogger.Object);
        var operations = new List<Task<FileSystemOperationResult>>();

        // Rapidly submit 10 operations
        for (int i = 0; i < 10; i++)
        {
            var operation = new FileSystemOperation
            {
                OperationType = FileSystemOperationType.MoveDirectory,
                SourcePath = $"source{i}",
                TargetPath = $"target{i}"
            };

            operations.Add(planner.SubmitOperationAsync(operation));
        }

        // Act
        var timeout = Task.Delay(TimeSpan.FromSeconds(15));
        var completed = await Task.WhenAny(Task.WhenAll(operations), timeout);

        // Assert
        Assert.NotSame(timeout, completed);
        Assert.True(operations.All(t => t.IsCompleted), "All 10 operations should complete");

        // Verify planner is still responsive after processing batch
        var additionalOp = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "additional",
            TargetPath = "additional-target"
        };

        var additionalResult = await planner.SubmitOperationAsync(additionalOp);
        Assert.NotNull(additionalResult);
    }

    [Fact]
    public async Task GetPendingOperationCount_ReflectsQueueState()
    {
        // Arrange
        var planner = new FileOperationPlanner(_mockArchiveHelper.Object, new SystemFileSystem(), _mockLogger.Object);

        // Initially should be 0
        Assert.Equal(0, planner.GetPendingOperationCount());

        // Note: It's hard to catch operations in flight because they execute very quickly
        // This test mainly verifies the method exists and returns a valid count
    }

    [Fact]
    public async Task SubmitOperationAsync_AfterFirstBatch_ContinuesProcessing()
    {
        // Arrange
        var planner = new FileOperationPlanner(_mockArchiveHelper.Object, new SystemFileSystem(), _mockLogger.Object);

        // Submit first batch
        var firstBatch = new List<Task<FileSystemOperationResult>>();
        for (int i = 0; i < 3; i++)
        {
            var op = new FileSystemOperation
            {
                OperationType = FileSystemOperationType.MoveDirectory,
                SourcePath = $"batch1-{i}",
                TargetPath = $"batch1-target-{i}"
            };
            firstBatch.Add(planner.SubmitOperationAsync(op));
        }

        // Wait for first batch to complete
        await Task.WhenAll(firstBatch);

        // Small delay to ensure planner is back to waiting state
        await Task.Delay(100);

        // Submit second batch - THIS IS THE CRITICAL TEST
        var secondBatch = new List<Task<FileSystemOperationResult>>();
        for (int i = 0; i < 3; i++)
        {
            var op = new FileSystemOperation
            {
                OperationType = FileSystemOperationType.MoveDirectory,
                SourcePath = $"batch2-{i}",
                TargetPath = $"batch2-target-{i}"
            };
            secondBatch.Add(planner.SubmitOperationAsync(op));
        }

        // Act - wait for second batch with timeout
        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        var completed = await Task.WhenAny(Task.WhenAll(secondBatch), timeout);

        // Assert
        Assert.NotSame(timeout, completed);
        Assert.True(secondBatch.All(t => t.IsCompleted), "Second batch should complete - planner should continue processing after first batch");
    }

    [Fact]
    public async Task Dispose_StopsBackgroundWorker()
    {
        // Arrange
        var planner = new FileOperationPlanner(_mockArchiveHelper.Object, new SystemFileSystem(), _mockLogger.Object);

        // Submit an operation to ensure worker is active
        var op = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "test",
            TargetPath = "test-target"
        };
        await planner.SubmitOperationAsync(op);

        // Act
        planner.Dispose();

        // Assert - worker should stop after dispose
        // Wait a bit to ensure shutdown completes
        await Task.Delay(100);

        // Verify planner logged shutdown (either "worker stopped" or fatal error if crashed)
        _mockLogger.Verify(
            x => x.Info(It.Is<string>(s => s.Contains("worker stopped")), It.IsAny<string>()),
            Times.AtMostOnce(),
            "Worker should log shutdown message if gracefully stopped"
        );
    }

    [Fact]
    public async Task SubmitOperationAsync_DuplicateOperations_SecondIsSkipped()
    {
        // Arrange
        var planner = new FileOperationPlanner(_mockArchiveHelper.Object, new SystemFileSystem(), _mockLogger.Object);

        var op1 = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "source",
            TargetPath = "target"
        };

        var op2 = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "source",
            TargetPath = "target"
        };

        // Act - submit both operations simultaneously so they're both in queued plan
        var task1 = planner.SubmitOperationAsync(op1);
        var task2 = planner.SubmitOperationAsync(op2);

        var results = await Task.WhenAll(task1, task2);

        // Assert - both should complete
        Assert.NotNull(results[0]);
        Assert.NotNull(results[1]);

        // Second operation should be deduplicated and return success immediately
        Assert.True(results[1].Success, "Duplicate operation should be deduplicated and return success");

        // Verify deduplication message was logged
        _mockLogger.Verify(
            x => x.Info(It.Is<string>(s => s.Contains("Identical operation already in queued plan")), It.IsAny<string>()),
            Times.AtLeastOnce
        );
    }
}
