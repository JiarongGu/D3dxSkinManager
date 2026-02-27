using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.TaskQueue.Models;
using D3dxSkinManager.Modules.TaskQueue.Repositories;

namespace D3dxSkinManager.Tests.Modules.TaskQueue.Repositories;

/// <summary>
/// Unit tests for TaskChainRepository (in-memory Dictionary implementation)
/// Tests business logic for managing task chains
/// </summary>
public class TaskChainRepositoryTests
{
    private readonly TaskChainRepository _repository;

    public TaskChainRepositoryTests()
    {
        _repository = new TaskChainRepository();
    }

    [Fact]
    public async Task AddAsync_WithValidChain_ShouldAdd()
    {
        // Arrange
        var chain = new TaskChainInfo
        {
            Id = "chain-1",
            ChainType = "import_chain",
            Status = TaskChainStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(chain);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("chain-1");

        var retrieved = await _repository.GetByIdAsync("chain-1");
        retrieved.Should().NotBeNull();
        retrieved!.ChainType.Should().Be("import_chain");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnChain()
    {
        // Arrange
        var chain = new TaskChainInfo
        {
            Id = "chain-2",
            ChainType = "batch_import",
            Status = TaskChainStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(chain);

        // Act
        var result = await _repository.GetByIdAsync("chain-2");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("chain-2");
        result.ChainType.Should().Be("batch_import");
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
    public async Task GetByProfileAsync_ShouldReturnAllChains()
    {
        // Arrange
        await _repository.AddAsync(new TaskChainInfo { Id = "chain-1", Status = TaskChainStatus.Pending, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "chain-2", Status = TaskChainStatus.Processing, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "chain-3", Status = TaskChainStatus.Completed, CreatedAt = DateTime.UtcNow });

        // Act
        var results = await _repository.GetByProfileAsync("profile-1");

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActiveAsync_ShouldReturnOnlyPendingAndProcessing()
    {
        // Arrange
        await _repository.AddAsync(new TaskChainInfo { Id = "pending", Status = TaskChainStatus.Pending, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "processing", Status = TaskChainStatus.Processing, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "completed", Status = TaskChainStatus.Completed, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "failed", Status = TaskChainStatus.Failed, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "cancelled", Status = TaskChainStatus.Cancelled, CreatedAt = DateTime.UtcNow });

        // Act
        var results = await _repository.GetActiveAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(c => c.Id == "pending");
        results.Should().Contain(c => c.Id == "processing");
        results.Should().NotContain(c => c.Id == "completed");
        results.Should().NotContain(c => c.Id == "failed");
        results.Should().NotContain(c => c.Id == "cancelled");
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldReturnOnlyChainsWithStatus()
    {
        // Arrange
        await _repository.AddAsync(new TaskChainInfo { Id = "completed-1", Status = TaskChainStatus.Completed, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "completed-2", Status = TaskChainStatus.Completed, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "pending-1", Status = TaskChainStatus.Pending, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "failed-1", Status = TaskChainStatus.Failed, CreatedAt = DateTime.UtcNow });

        // Act
        var results = await _repository.GetByStatusAsync(TaskChainStatus.Completed);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(c => c.Id == "completed-1");
        results.Should().Contain(c => c.Id == "completed-2");
        results.Should().NotContain(c => c.Id == "pending-1");
        results.Should().NotContain(c => c.Id == "failed-1");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateChain()
    {
        // Arrange
        var chain = new TaskChainInfo
        {
            Id = "update-test",
            Status = TaskChainStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(chain);

        // Act - Update the status
        chain.Status = TaskChainStatus.Processing;
        chain.StartedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(chain);

        // Assert
        var retrieved = await _repository.GetByIdAsync("update-test");
        retrieved!.Status.Should().Be(TaskChainStatus.Processing);
        retrieved.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveChain()
    {
        // Arrange
        var chain = new TaskChainInfo { Id = "delete-test", Status = TaskChainStatus.Pending, CreatedAt = DateTime.UtcNow };
        await _repository.AddAsync(chain);

        // Act
        await _repository.DeleteAsync("delete-test");

        // Assert
        var retrieved = await _repository.GetByIdAsync("delete-test");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ClearCompletedAsync_ShouldRemoveOldCompletedAndFailedChains()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var yesterday = now.AddDays(-1);
        var twoDaysAgo = now.AddDays(-2);

        await _repository.AddAsync(new TaskChainInfo
        {
            Id = "old-completed",
            Status = TaskChainStatus.Completed,
            CreatedAt = twoDaysAgo,
            CompletedAt = twoDaysAgo
        });
        await _repository.AddAsync(new TaskChainInfo
        {
            Id = "old-failed",
            Status = TaskChainStatus.Failed,
            CreatedAt = twoDaysAgo,
            CompletedAt = twoDaysAgo
        });
        await _repository.AddAsync(new TaskChainInfo
        {
            Id = "recent-completed",
            Status = TaskChainStatus.Completed,
            CreatedAt = now,
            CompletedAt = now
        });
        await _repository.AddAsync(new TaskChainInfo
        {
            Id = "pending",
            Status = TaskChainStatus.Pending,
            CreatedAt = twoDaysAgo
        });

        // Act - Clear chains older than yesterday
        var removedCount = await _repository.ClearCompletedAsync(yesterday);

        // Assert
        removedCount.Should().Be(2); // old-completed and old-failed

        var oldCompleted = await _repository.GetByIdAsync("old-completed");
        var oldFailed = await _repository.GetByIdAsync("old-failed");
        var recentCompleted = await _repository.GetByIdAsync("recent-completed");
        var pending = await _repository.GetByIdAsync("pending");

        oldCompleted.Should().BeNull();
        oldFailed.Should().BeNull();
        recentCompleted.Should().NotBeNull(); // Should remain (newer than cutoff)
        pending.Should().NotBeNull(); // Should remain (not completed/failed)
    }

    [Fact]
    public async Task AddAsync_WithDuplicateId_ShouldOverwrite()
    {
        // Arrange
        var chain1 = new TaskChainInfo
        {
            Id = "same-id",
            ChainType = "original",
            Status = TaskChainStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        var chain2 = new TaskChainInfo
        {
            Id = "same-id",
            ChainType = "updated",
            Status = TaskChainStatus.Processing,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _repository.AddAsync(chain1);
        await _repository.AddAsync(chain2);

        // Assert
        var retrieved = await _repository.GetByIdAsync("same-id");
        retrieved.Should().NotBeNull();
        retrieved!.ChainType.Should().Be("updated");
        retrieved.Status.Should().Be(TaskChainStatus.Processing);
    }

    [Fact]
    public async Task GetActiveAsync_WithNoActiveChains_ShouldReturnEmpty()
    {
        // Arrange
        await _repository.AddAsync(new TaskChainInfo { Id = "completed", Status = TaskChainStatus.Completed, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "failed", Status = TaskChainStatus.Failed, CreatedAt = DateTime.UtcNow });

        // Act
        var results = await _repository.GetActiveAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByStatusAsync_WithNoMatchingStatus_ShouldReturnEmpty()
    {
        // Arrange
        await _repository.AddAsync(new TaskChainInfo { Id = "pending", Status = TaskChainStatus.Pending, CreatedAt = DateTime.UtcNow });
        await _repository.AddAsync(new TaskChainInfo { Id = "processing", Status = TaskChainStatus.Processing, CreatedAt = DateTime.UtcNow });

        // Act
        var results = await _repository.GetByStatusAsync(TaskChainStatus.Cancelled);

        // Assert
        results.Should().BeEmpty();
    }
}
