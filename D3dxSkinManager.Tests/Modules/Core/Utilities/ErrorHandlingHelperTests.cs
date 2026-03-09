using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Modules.Core.Utilities;

/// <summary>
/// Unit tests for ErrorHandlingHelper
/// Tests standardized error handling patterns and OperationException wrapping
/// </summary>
public class ErrorHandlingHelperTests
{
    private readonly Mock<ILogHelper> _mockLogger;

    public ErrorHandlingHelperTests()
    {
        _mockLogger = new Mock<ILogHelper>();
    }

    [Fact]
    public async Task ExecuteWithErrorHandlingAsync_WithSuccessfulOperation_ShouldReturnResult()
    {
        // Arrange
        var expectedResult = 42;

        // Act
        var result = await ErrorHandlingHelper.ExecuteWithErrorHandlingAsync(
            () => Task.FromResult(expectedResult),
            _mockLogger.Object,
            "test operation",
            "TestModule"
        );

        // Assert
        result.Should().Be(expectedResult);
        _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteWithErrorHandlingAsync_WithOperationException_ShouldRethrowAndLog()
    {
        // Arrange
        var operationException = new OperationException("TEST_ERROR", (Dictionary<string, string>?)null, "Test error");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationException>(() =>
            ErrorHandlingHelper.ExecuteWithErrorHandlingAsync<int>(
                () => throw operationException,
                _mockLogger.Object,
                "test operation",
                "TestModule",
                "FALLBACK_ERROR"
            )
        );

        exception.Code.Should().Be("TEST_ERROR");
        _mockLogger.Verify(l => l.Error(
            It.Is<string>(s => s.Contains("test operation") && s.Contains("TEST_ERROR")),
            "TestModule",
            It.IsAny<OperationException>()
        ), Times.Once);
    }

    [Fact]
    public async Task ExecuteWithErrorHandlingAsync_WithGenericException_ShouldWrapInOperationException()
    {
        // Arrange
        var genericException = new InvalidOperationException("Something went wrong");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationException>(() =>
            ErrorHandlingHelper.ExecuteWithErrorHandlingAsync<int>(
                () => throw genericException,
                _mockLogger.Object,
                "test operation",
                "TestModule",
                "CUSTOM_ERROR",
                new Dictionary<string, string> { { "param1", "value1" } }
            )
        );

        exception.Code.Should().Be("CUSTOM_ERROR");
        exception.Parameters.Should().ContainKey("param1");
        exception.Parameters!["param1"].Should().Be("value1");
        exception.InnerException.Should().Be(genericException);

        _mockLogger.Verify(l => l.Error(
            It.Is<string>(s => s.Contains("Unexpected error") && s.Contains("test operation")),
            "TestModule",
            genericException
        ), Times.Once);
    }

    [Fact]
    public async Task TryExecuteAsync_WithSuccessfulOperation_ShouldReturnTrue()
    {
        // Arrange
        var operationExecuted = false;

        // Act
        var result = await ErrorHandlingHelper.TryExecuteAsync(
            async () =>
            {
                await Task.Delay(1);
                operationExecuted = true;
            },
            _mockLogger.Object,
            "test operation",
            "TestModule"
        );

        // Assert
        result.Should().BeTrue();
        operationExecuted.Should().BeTrue();
        _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task TryExecuteAsync_WithException_ShouldReturnFalseAndLog()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = await ErrorHandlingHelper.TryExecuteAsync(
            () => throw exception,
            _mockLogger.Object,
            "test operation",
            "TestModule"
        );

        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(l => l.Error(
            It.Is<string>(s => s.Contains("test operation") && s.Contains("failed")),
            "TestModule",
            exception
        ), Times.Once);
    }

    [Fact]
    public async Task TryExecuteWithDefaultAsync_WithSuccessfulOperation_ShouldReturnResult()
    {
        // Arrange
        var expectedResult = "success";

        // Act
        var result = await ErrorHandlingHelper.TryExecuteWithDefaultAsync(
            () => Task.FromResult(expectedResult),
            _mockLogger.Object,
            "test operation",
            "TestModule",
            "default"
        );

        // Assert
        result.Should().Be(expectedResult);
        _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task TryExecuteWithDefaultAsync_WithException_ShouldReturnDefaultAndLog()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var defaultValue = "default";

        // Act
        var result = await ErrorHandlingHelper.TryExecuteWithDefaultAsync(
            () => throw exception,
            _mockLogger.Object,
            "test operation",
            "TestModule",
            defaultValue
        );

        // Assert
        result.Should().Be(defaultValue);
        _mockLogger.Verify(l => l.Error(
            It.Is<string>(s => s.Contains("test operation")),
            "TestModule",
            exception
        ), Times.Once);
    }

    [Fact]
    public void ExecuteWithErrorHandling_Sync_WithSuccess_ShouldReturnResult()
    {
        // Arrange
        var expectedResult = 42;

        // Act
        var result = ErrorHandlingHelper.ExecuteWithErrorHandling(
            () => expectedResult,
            _mockLogger.Object,
            "test operation",
            "TestModule"
        );

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void ExecuteWithErrorHandling_Sync_WithException_ShouldWrapAndThrow()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act & Assert
        var operationException = Assert.Throws<OperationException>(() =>
            ErrorHandlingHelper.ExecuteWithErrorHandling<int>(
                () => throw exception,
                _mockLogger.Object,
                "test operation",
                "TestModule",
                "CUSTOM_ERROR"
            )
        );

        operationException.Code.Should().Be("CUSTOM_ERROR");
        operationException.InnerException.Should().Be(exception);
    }

    [Fact]
    public void TryExecute_Sync_WithSuccess_ShouldReturnTrue()
    {
        // Arrange
        var operationExecuted = false;

        // Act
        var result = ErrorHandlingHelper.TryExecute(
            () => operationExecuted = true,
            _mockLogger.Object,
            "test operation",
            "TestModule"
        );

        // Assert
        result.Should().BeTrue();
        operationExecuted.Should().BeTrue();
    }

    [Fact]
    public void TryExecute_Sync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = ErrorHandlingHelper.TryExecute(
            () => throw exception,
            _mockLogger.Object,
            "test operation",
            "TestModule"
        );

        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(l => l.Error(
            It.IsAny<string>(),
            "TestModule",
            exception
        ), Times.Once);
    }

    [Fact]
    public async Task TryExecuteWithDefaultAsync_WithNullDefault_ShouldReturnNull()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = await ErrorHandlingHelper.TryExecuteWithDefaultAsync<string>(
            () => throw exception,
            _mockLogger.Object,
            "test operation",
            "TestModule",
            null
        );

        // Assert
        result.Should().BeNull();
    }
}
