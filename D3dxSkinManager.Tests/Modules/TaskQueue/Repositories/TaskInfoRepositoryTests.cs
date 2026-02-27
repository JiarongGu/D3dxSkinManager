using FluentAssertions;
using D3dxSkinManager.Modules.TaskQueue.Models;
using D3dxSkinManager.Modules.TaskQueue.Repositories;
using TaskStatus = D3dxSkinManager.Modules.TaskQueue.Models.TaskStatus;

namespace D3dxSkinManager.Tests.Modules.TaskQueue.Repositories;

/// <summary>
/// Unit tests for TaskInfoRepository (in-memory Dictionary implementation)
/// Tests business logic for managing individual tasks within chains
/// </summary>
public class TaskInfoRepositoryTests
{
    private readonly TaskInfoRepository _repository;

    public TaskInfoRepositoryTests()
    {
        _repository = new TaskInfoRepository();
    }

    [Fact]
    public async Task AddAsync_WithValidTask_ShouldAdd()
    {
        // Arrange
        var task = new TaskInfo
        {
            Id = "task-1",
            Type = "EXTRACT_ARCHIVE",
            TaskChainId = "chain-1",
            NodeId = "node-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(task);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("task-1");

        var retrieved = await _repository.GetByIdAsync("task-1");
        retrieved.Should().NotBeNull();
        retrieved!.Type.Should().Be("EXTRACT_ARCHIVE");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnTask()
    {
        // Arrange
        var task = new TaskInfo
        {
            Id = "task-2",
            Type = "HASH_FILE",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(task);

        // Act
        var result = await _repository.GetByIdAsync("task-2");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("task-2");
        result.Type.Should().Be("HASH_FILE");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByChainIdAsync_ShouldReturnTasksInOrder()
    {
        // Arrange
        var now = DateTime.UtcNow;
        await _repository.AddAsync(new TaskInfo
        {
            Id = "task-3",
            Type = "STEP_3",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = now.AddSeconds(2)
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "task-1",
            Type = "STEP_1",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = now
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "task-2",
            Type = "STEP_2",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = now.AddSeconds(1)
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "other-chain-task",
            Type = "OTHER",
            TaskChainId = "chain-2",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = now
        });

        // Act
        var results = await _repository.GetByChainIdAsync("chain-1");

        // Assert
        results.Should().HaveCount(3);
        results[0].Id.Should().Be("task-1"); // Ordered by CreatedAt
        results[1].Id.Should().Be("task-2");
        results[2].Id.Should().Be("task-3");
    }

    [Fact]
    public async Task GetNextPendingInChainAsync_ShouldReturnOldestPending()
    {
        // Arrange
        var now = DateTime.UtcNow;
        await _repository.AddAsync(new TaskInfo
        {
            Id = "completed-task",
            Type = "COMPLETED",
            TaskChainId = "chain-1",
            Status = TaskStatus.Completed,
            Input = "{}",
            CreatedAt = now
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "pending-2",
            Type = "PENDING_2",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = now.AddSeconds(2)
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "pending-1",
            Type = "PENDING_1",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = now.AddSeconds(1)
        });

        // Act
        var result = await _repository.GetNextPendingInChainAsync("chain-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("pending-1"); // Oldest pending task
    }

    [Fact]
    public async Task GetNextPendingInChainAsync_WithNoPending_ShouldReturnNull()
    {
        // Arrange
        await _repository.AddAsync(new TaskInfo
        {
            Id = "completed",
            Type = "COMPLETED",
            TaskChainId = "chain-1",
            Status = TaskStatus.Completed,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var result = await _repository.GetNextPendingInChainAsync("chain-1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProcessingInChainAsync_ShouldReturnProcessingTask()
    {
        // Arrange
        await _repository.AddAsync(new TaskInfo
        {
            Id = "pending",
            Type = "PENDING",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "processing",
            Type = "PROCESSING",
            TaskChainId = "chain-1",
            Status = TaskStatus.Processing,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var result = await _repository.GetProcessingInChainAsync("chain-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("processing");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTask()
    {
        // Arrange
        var task = new TaskInfo
        {
            Id = "update-test",
            Type = "TEST",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(task);

        // Act - Update the task
        task.Status = TaskStatus.Processing;
        task.StartedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(task);

        // Assert
        var retrieved = await _repository.GetByIdAsync("update-test");
        retrieved!.Status.Should().Be(TaskStatus.Processing);
        retrieved.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateStatus()
    {
        // Arrange
        var task = new TaskInfo
        {
            Id = "status-test",
            Type = "TEST",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(task);

        // Act
        await _repository.UpdateStatusAsync("status-test", TaskStatus.Processing, "Processing now");

        // Assert
        var retrieved = await _repository.GetByIdAsync("status-test");
        retrieved!.Status.Should().Be(TaskStatus.Processing);
        retrieved.StartedAt.Should().NotBeNull(); // Should be set automatically
    }

    [Fact]
    public async Task CompleteAsync_ShouldMarkAsCompleted()
    {
        // Arrange
        var task = new TaskInfo
        {
            Id = "complete-test",
            Type = "TEST",
            TaskChainId = "chain-1",
            Status = TaskStatus.Processing,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(task);

        // Act
        var completedAt = DateTime.UtcNow;
        await _repository.CompleteAsync("complete-test", "{\"result\":\"success\"}", completedAt);

        // Assert
        var retrieved = await _repository.GetByIdAsync("complete-test");
        retrieved!.Status.Should().Be(TaskStatus.Completed);
        retrieved.Output.Should().Be("{\"result\":\"success\"}");
        retrieved.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public async Task FailAsync_ShouldMarkAsFailed()
    {
        // Arrange
        var task = new TaskInfo
        {
            Id = "fail-test",
            Type = "TEST",
            TaskChainId = "chain-1",
            Status = TaskStatus.Processing,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(task);

        // Act
        var completedAt = DateTime.UtcNow;
        await _repository.FailAsync("fail-test", "Something went wrong", completedAt);

        // Assert
        var retrieved = await _repository.GetByIdAsync("fail-test");
        retrieved!.Status.Should().Be(TaskStatus.Failed);
        retrieved.ErrorMessage.Should().Be("Something went wrong");
        retrieved.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTask()
    {
        // Arrange
        var task = new TaskInfo
        {
            Id = "delete-test",
            Type = "TEST",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(task);

        // Act
        await _repository.DeleteAsync("delete-test");

        // Assert
        var retrieved = await _repository.GetByIdAsync("delete-test");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteByChainIdAsync_ShouldRemoveAllTasksInChain()
    {
        // Arrange
        await _repository.AddAsync(new TaskInfo
        {
            Id = "chain1-task1",
            Type = "TEST",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "chain1-task2",
            Type = "TEST",
            TaskChainId = "chain-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "chain2-task1",
            Type = "TEST",
            TaskChainId = "chain-2",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        await _repository.DeleteByChainIdAsync("chain-1");

        // Assert
        var chain1Task1 = await _repository.GetByIdAsync("chain1-task1");
        var chain1Task2 = await _repository.GetByIdAsync("chain1-task2");
        var chain2Task1 = await _repository.GetByIdAsync("chain2-task1");

        chain1Task1.Should().BeNull();
        chain1Task2.Should().BeNull();
        chain2Task1.Should().NotBeNull(); // Should remain
    }

    [Fact]
    public async Task GetByNodeAsync_ShouldReturnTaskWithMatchingNode()
    {
        // Arrange
        await _repository.AddAsync(new TaskInfo
        {
            Id = "task-1",
            Type = "TEST",
            TaskChainId = "chain-1",
            NodeId = "node-1",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        });
        await _repository.AddAsync(new TaskInfo
        {
            Id = "task-2",
            Type = "TEST",
            TaskChainId = "chain-1",
            NodeId = "node-2",
            Status = TaskStatus.Pending,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var result = await _repository.GetByNodeAsync("chain-1", "node-2");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("task-2");
        result.NodeId.Should().Be("node-2");
    }

    [Fact]
    public async Task GetByNodeAsync_WithNonExisting_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByNodeAsync("chain-1", "non-existing-node");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProgressAsync_ShouldNotThrow()
    {
        // Arrange
        var task = new TaskInfo
        {
            Id = "progress-test",
            Type = "TEST",
            TaskChainId = "chain-1",
            Status = TaskStatus.Processing,
            Input = "{}",
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(task);

        // Act & Assert - Should not throw (no-op in current implementation)
        await _repository.UpdateProgressAsync("progress-test", 0.5f, "50% complete");
    }
}
