using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Models;

namespace D3dxSkinManager.Tests.Modules.Workflow.Entities;

/// <summary>
/// Unit tests for WorkflowEntityMappers
/// Tests entity-domain conversion without external dependencies
/// </summary>
public class WorkflowEntityMappersTests
{
    [Fact]
    public void ToDomain_WithValidEntity_ShouldConvertCorrectly()
    {
        // Arrange
        var entity = new WorkflowEntity
        {
            Id = "wf-123",
            Type = "MOD_IMPORT",
            Status = WorkflowStatus.Processing,
            Context = "{\"modId\":\"abc123\",\"step\":2}",
            ErrorMessage = null,
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            CompletedAt = null
        };

        // Act
        var domain = entity.ToDomain();

        // Assert
        domain.Should().NotBeNull();
        domain.Id.Should().Be("wf-123");
        domain.Type.Should().Be("MOD_IMPORT");
        domain.Status.Should().Be(WorkflowStatus.Processing);
        domain.Context.Should().Be("{\"modId\":\"abc123\",\"step\":2}");
        domain.ErrorMessage.Should().BeNull();
        domain.CreatedAt.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        domain.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void ToDomain_WithCompletedWorkflow_ShouldMapCompletedAt()
    {
        // Arrange
        var entity = new WorkflowEntity
        {
            Id = "wf-123",
            Type = "BATCH_EXPORT",
            Status = WorkflowStatus.Completed,
            Context = "{}",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var domain = entity.ToDomain();

        // Assert
        domain.CompletedAt.Should().Be(new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc));
        domain.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public void ToDomain_WithFailedWorkflow_ShouldMapErrorMessage()
    {
        // Arrange
        var entity = new WorkflowEntity
        {
            Id = "wf-123",
            Type = "MOD_IMPORT",
            Status = WorkflowStatus.Failed,
            Context = "{}",
            ErrorMessage = "Import failed: File not found",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        // Act
        var domain = entity.ToDomain();

        // Assert
        domain.Status.Should().Be(WorkflowStatus.Failed);
        domain.ErrorMessage.Should().Be("Import failed: File not found");
    }

    [Fact]
    public void ToEntity_WithValidDomain_ShouldConvertCorrectly()
    {
        // Arrange
        var domain = new WorkflowInfo
        {
            Id = "wf-456",
            Type = "BATCH_EXPORT",
            Status = WorkflowStatus.Pending,
            Context = "{\"targetPath\":\"/export\"}",
            ErrorMessage = null,
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            CompletedAt = null
        };

        // Act
        var entity = domain.ToEntity();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be("wf-456");
        entity.Type.Should().Be("BATCH_EXPORT");
        entity.Status.Should().Be(WorkflowStatus.Pending);
        entity.Context.Should().Be("{\"targetPath\":\"/export\"}");
        entity.ErrorMessage.Should().BeNull();
        entity.CreatedAt.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        entity.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WithAllStatusTypes_ShouldPreserveStatus()
    {
        // Arrange & Act & Assert - Test all workflow statuses
        var statuses = new[]
        {
            WorkflowStatus.Pending,
            WorkflowStatus.Processing,
            WorkflowStatus.WaitingForInput,
            WorkflowStatus.Paused,
            WorkflowStatus.Completed,
            WorkflowStatus.Failed,
            WorkflowStatus.Cancelled,
            WorkflowStatus.Deleting
        };

        foreach (var status in statuses)
        {
            var domain = new WorkflowInfo
            {
                Id = $"wf-{status}",
                Type = "TEST",
                Status = status,
                Context = "{}"
            };

            var entity = domain.ToEntity();
            entity.Status.Should().Be(status, $"Status {status} should be preserved");
        }
    }

    [Fact]
    public void ToDomainList_WithMultipleWorkflows_ShouldConvertAll()
    {
        // Arrange
        var entities = new List<WorkflowEntity>
        {
            new WorkflowEntity
            {
                Id = "wf-1",
                Type = "MOD_IMPORT",
                Status = WorkflowStatus.Pending,
                Context = "{}",
                CreatedAt = new DateTime(2024, 1, 1)
            },
            new WorkflowEntity
            {
                Id = "wf-2",
                Type = "BATCH_EXPORT",
                Status = WorkflowStatus.Processing,
                Context = "{\"progress\":50}",
                CreatedAt = new DateTime(2024, 1, 2)
            },
            new WorkflowEntity
            {
                Id = "wf-3",
                Type = "MOD_UPDATE",
                Status = WorkflowStatus.Completed,
                Context = "{}",
                CreatedAt = new DateTime(2024, 1, 3),
                CompletedAt = new DateTime(2024, 1, 3, 1, 0, 0)
            }
        };

        // Act
        var domainList = entities.ToDomainList();

        // Assert
        domainList.Should().HaveCount(3);
        domainList[0].Id.Should().Be("wf-1");
        domainList[0].Status.Should().Be(WorkflowStatus.Pending);
        domainList[1].Id.Should().Be("wf-2");
        domainList[1].Status.Should().Be(WorkflowStatus.Processing);
        domainList[1].Context.Should().Contain("progress");
        domainList[2].Id.Should().Be("wf-3");
        domainList[2].Status.Should().Be(WorkflowStatus.Completed);
        domainList[2].CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void ToDomainList_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var entities = new List<WorkflowEntity>();

        // Act
        var domainList = entities.ToDomainList();

        // Assert
        domainList.Should().NotBeNull();
        domainList.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_EntityToDomainToEntity_ShouldPreserveData()
    {
        // Arrange
        var originalEntity = new WorkflowEntity
        {
            Id = "wf-789",
            Type = "MOD_IMPORT",
            Status = WorkflowStatus.Processing,
            Context = "{\"modId\":\"xyz789\",\"step\":3,\"totalSteps\":5}",
            ErrorMessage = null,
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            CompletedAt = null
        };

        // Act
        var domain = originalEntity.ToDomain();
        var roundTrippedEntity = domain.ToEntity();

        // Assert
        roundTrippedEntity.Id.Should().Be(originalEntity.Id);
        roundTrippedEntity.Type.Should().Be(originalEntity.Type);
        roundTrippedEntity.Status.Should().Be(originalEntity.Status);
        roundTrippedEntity.Context.Should().Be(originalEntity.Context);
        roundTrippedEntity.ErrorMessage.Should().Be(originalEntity.ErrorMessage);
        roundTrippedEntity.CreatedAt.Should().Be(originalEntity.CreatedAt);
        roundTrippedEntity.CompletedAt.Should().Be(originalEntity.CompletedAt);
    }

    [Fact]
    public void RoundTrip_DomainToEntityToDomain_ShouldPreserveData()
    {
        // Arrange
        var originalDomain = new WorkflowInfo
        {
            Id = "wf-999",
            Type = "BATCH_EXPORT",
            Status = WorkflowStatus.Completed,
            Context = "{\"exportedCount\":42,\"path\":\"/exports/batch-123\"}",
            ErrorMessage = null,
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2024, 1, 1, 13, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var entity = originalDomain.ToEntity();
        var roundTrippedDomain = entity.ToDomain();

        // Assert
        roundTrippedDomain.Id.Should().Be(originalDomain.Id);
        roundTrippedDomain.Type.Should().Be(originalDomain.Type);
        roundTrippedDomain.Status.Should().Be(originalDomain.Status);
        roundTrippedDomain.Context.Should().Be(originalDomain.Context);
        roundTrippedDomain.ErrorMessage.Should().Be(originalDomain.ErrorMessage);
        roundTrippedDomain.CreatedAt.Should().Be(originalDomain.CreatedAt);
        roundTrippedDomain.CompletedAt.Should().Be(originalDomain.CompletedAt);
    }

    [Fact]
    public void ToDomain_WithEmptyContext_ShouldPreserveEmptyObject()
    {
        // Arrange
        var entity = new WorkflowEntity
        {
            Id = "wf-empty",
            Type = "TEST",
            Status = WorkflowStatus.Pending,
            Context = "{}",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var domain = entity.ToDomain();

        // Assert
        domain.Context.Should().Be("{}");
    }

    [Fact]
    public void ToEntity_WithComplexContext_ShouldPreserveContext()
    {
        // Arrange
        var complexContext = "{\"nested\":{\"level2\":{\"level3\":\"value\"}},\"array\":[1,2,3],\"bool\":true}";
        var domain = new WorkflowInfo
        {
            Id = "wf-complex",
            Type = "TEST",
            Status = WorkflowStatus.Pending,
            Context = complexContext
        };

        // Act
        var entity = domain.ToEntity();

        // Assert
        entity.Context.Should().Be(complexContext);
    }

    [Fact]
    public void ToDomainList_ShouldMaintainOrderOfEntities()
    {
        // Arrange
        var entities = Enumerable.Range(1, 10)
            .Select(i => new WorkflowEntity
            {
                Id = $"wf-{i:D3}",
                Type = "TEST",
                Status = WorkflowStatus.Pending,
                Context = "{}",
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            })
            .ToList();

        // Act
        var domainList = entities.ToDomainList();

        // Assert
        domainList.Should().HaveCount(10);
        for (int i = 0; i < 10; i++)
        {
            domainList[i].Id.Should().Be($"wf-{i + 1:D3}");
        }
    }

    [Fact]
    public void ToDomain_WithNullErrorMessage_ShouldMapToNull()
    {
        // Arrange
        var entity = new WorkflowEntity
        {
            Id = "wf-no-error",
            Type = "TEST",
            Status = WorkflowStatus.Completed,
            Context = "{}",
            ErrorMessage = null
        };

        // Act
        var domain = entity.ToDomain();

        // Assert
        domain.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WithEmptyStringErrorMessage_ShouldPreserveEmptyString()
    {
        // Arrange
        var domain = new WorkflowInfo
        {
            Id = "wf-empty-error",
            Type = "TEST",
            Status = WorkflowStatus.Failed,
            Context = "{}",
            ErrorMessage = ""
        };

        // Act
        var entity = domain.ToEntity();

        // Assert
        entity.ErrorMessage.Should().Be("");
    }
}
