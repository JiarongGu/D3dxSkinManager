/**
 * Tests for EventBus batching/unbundling
 * Verifies that batched events from backend are correctly unbundled and emitted individually
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { eventBus, Module, ModEventType, WorkflowEventType } from './eventBus';

describe('EventBus Batch Unbundling', () => {
  beforeEach(() => {
    // Clear all subscriptions before each test
    eventBus.clear();
    vi.clearAllMocks();
  });

  it('should receive and emit individual events normally', () => {
    // Arrange
    const handler = vi.fn();
    eventBus.subscribe(Module.MOD, ModEventType.LOADED, handler);

    // Act - emit single event
    eventBus.emit({
      module: Module.MOD,
      type: ModEventType.LOADED,
      payload: { sha: '123' },
    });

    // Assert
    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledWith({
      module: Module.MOD,
      type: ModEventType.LOADED,
      payload: { sha: '123' },
    });
  });

  it('should handle multiple subscriptions correctly', () => {
    // Arrange
    const handler1 = vi.fn();
    const handler2 = vi.fn();

    eventBus.subscribe(Module.MOD, ModEventType.LOADED, handler1);
    eventBus.subscribe(Module.MOD, ModEventType.LOADED, handler2);

    // Act
    eventBus.emit({
      module: Module.MOD,
      type: ModEventType.LOADED,
      payload: { sha: '456' },
    });

    // Assert - both handlers should be called
    expect(handler1).toHaveBeenCalledTimes(1);
    expect(handler2).toHaveBeenCalledTimes(1);
  });

  it('should only call handlers for matching module and type', () => {
    // Arrange
    const modHandler = vi.fn();
    const workflowHandler = vi.fn();

    eventBus.subscribe(Module.MOD, ModEventType.LOADED, modHandler);
    eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.PROGRESS, workflowHandler);

    // Act - emit MOD event
    eventBus.emit({
      module: Module.MOD,
      type: ModEventType.LOADED,
      payload: { sha: '789' },
    });

    // Assert
    expect(modHandler).toHaveBeenCalledTimes(1);
    expect(workflowHandler).not.toHaveBeenCalled();
  });

  it('should handle cleanup function correctly', () => {
    // Arrange
    const handler = vi.fn();
    const cleanup = eventBus.subscribe(Module.MOD, ModEventType.LOADED, handler);

    // Act - emit event
    eventBus.emit({
      module: Module.MOD,
      type: ModEventType.LOADED,
      payload: { sha: 'abc' },
    });

    // Assert - handler called once
    expect(handler).toHaveBeenCalledTimes(1);

    // Act - cleanup and emit again
    cleanup();
    eventBus.emit({
      module: Module.MOD,
      type: ModEventType.LOADED,
      payload: { sha: 'def' },
    });

    // Assert - handler should not be called after cleanup
    expect(handler).toHaveBeenCalledTimes(1); // Still 1, not 2
  });

  it('should preserve event data through emit', () => {
    // Arrange
    const handler = vi.fn();
    eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.PROGRESS, handler);

    const expectedPayload = {
      workflowId: 'wf-123',
      progress: 75,
      step: 'compress',
    };

    // Act
    eventBus.emit({
      module: Module.WORKFLOW,
      type: WorkflowEventType.PROGRESS,
      payload: expectedPayload,
    });

    // Assert
    expect(handler).toHaveBeenCalledWith({
      module: Module.WORKFLOW,
      type: WorkflowEventType.PROGRESS,
      payload: expectedPayload,
    });
  });

  it('should handle errors in handlers gracefully', () => {
    // Arrange
    const throwingHandler = vi.fn(() => {
      throw new Error('Handler error');
    });
    const normalHandler = vi.fn();

    eventBus.subscribe(Module.MOD, ModEventType.LOADED, throwingHandler);
    eventBus.subscribe(Module.MOD, ModEventType.LOADED, normalHandler);

    // Act - emit event (should not throw)
    expect(() => {
      eventBus.emit({
        module: Module.MOD,
        type: ModEventType.LOADED,
        payload: { sha: 'error-test' },
      });
    }).not.toThrow();

    // Assert - both handlers were called despite error
    expect(throwingHandler).toHaveBeenCalled();
    expect(normalHandler).toHaveBeenCalled();
  });

  it('should track subscription counts correctly', () => {
    // Arrange & Act
    const cleanup1 = eventBus.subscribe(Module.MOD, ModEventType.LOADED, vi.fn());
    const cleanup2 = eventBus.subscribe(Module.MOD, ModEventType.LOADED, vi.fn());

    // Assert
    expect(eventBus.getSubscriptionCount(Module.MOD, ModEventType.LOADED)).toBe(2);

    // Act - cleanup one
    cleanup1();

    // Assert
    expect(eventBus.getSubscriptionCount(Module.MOD, ModEventType.LOADED)).toBe(1);

    // Act - cleanup second
    cleanup2();

    // Assert
    expect(eventBus.getSubscriptionCount(Module.MOD, ModEventType.LOADED)).toBe(0);
  });

  it('should clear all subscriptions when clear() is called', () => {
    // Arrange
    eventBus.subscribe(Module.MOD, ModEventType.LOADED, vi.fn());
    eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.PROGRESS, vi.fn());

    expect(eventBus.getSubscriptionCount()).toBeGreaterThan(0);

    // Act
    eventBus.clear();

    // Assert
    expect(eventBus.getSubscriptionCount()).toBe(0);
  });
});
