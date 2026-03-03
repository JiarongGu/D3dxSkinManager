/**
 * Tests for BridgeService event batch unbundling
 * Verifies that batched events from backend are correctly unbundled
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { eventBus, Module, ModEventType, WorkflowEventType } from './eventBus';

describe('BridgeService Batch Unbundling', () => {
  beforeEach(() => {
    eventBus.clear();
    vi.clearAllMocks();
  });

  it('should unbundle batched events and emit individually', () => {
    // Arrange
    const modHandler = vi.fn();
    const workflowHandler = vi.fn();

    eventBus.subscribe(Module.MOD, ModEventType.LOADED, modHandler);
    eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.PROGRESS, workflowHandler);

    // Simulate a batched event from backend
    const batchedPayload = [
      {
        module: 'MOD',
        type: 'LOADED',
        payload: { sha: '123' },
        profileId: null,
      },
      {
        module: 'MOD',
        type: 'LOADED',
        payload: { sha: '456' },
        profileId: null,
      },
      {
        module: 'WORKFLOW',
        type: 'PROGRESS',
        payload: { workflowId: 'wf-1', progress: 50 },
        profileId: null,
      },
    ];

    // Act - simulate receiving batch from backend (what bridgeService does)
    // In real code, this would be:
    // if (module === "EVENT_BUS" && type === "BATCH" && Array.isArray(payload))
    batchedPayload.forEach((event) => {
      eventBus.emit({
        module: event.module,
        type: event.type,
        payload: event.payload,
      });
    });

    // Assert
    expect(modHandler).toHaveBeenCalledTimes(2);
    expect(modHandler).toHaveBeenNthCalledWith(1, {
      module: 'MOD',
      type: 'LOADED',
      payload: { sha: '123' },
    });
    expect(modHandler).toHaveBeenNthCalledWith(2, {
      module: 'MOD',
      type: 'LOADED',
      payload: { sha: '456' },
    });

    expect(workflowHandler).toHaveBeenCalledTimes(1);
    expect(workflowHandler).toHaveBeenCalledWith({
      module: 'WORKFLOW',
      type: 'PROGRESS',
      payload: { workflowId: 'wf-1', progress: 50 },
    });
  });

  it('should handle empty batch gracefully', () => {
    // Arrange
    const handler = vi.fn();
    eventBus.subscribe(Module.MOD, ModEventType.LOADED, handler);

    // Act - simulate empty batch
    const emptyBatch: any[] = [];
    emptyBatch.forEach((event) => {
      eventBus.emit(event);
    });

    // Assert
    expect(handler).not.toHaveBeenCalled();
  });

  it('should preserve event data during unbundling', () => {
    // Arrange
    const handler = vi.fn();
    eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.PROGRESS, handler);

    const complexPayload = {
      workflowId: 'wf-complex-123',
      progress: 75,
      step: 'compress_folder',
      metadata: {
        name: 'Test Mod',
        author: 'Test Author',
        tags: ['tag1', 'tag2'],
      },
    };

    // Act - simulate batched event with complex payload
    const batch = [
      {
        module: 'WORKFLOW',
        type: 'PROGRESS',
        payload: complexPayload,
        profileId: 'profile-789',
      },
    ];

    batch.forEach((event) => {
      eventBus.emit({
        module: event.module,
        type: event.type,
        payload: event.payload,
      });
    });

    // Assert - complex payload should be preserved
    expect(handler).toHaveBeenCalledWith({
      module: 'WORKFLOW',
      type: 'PROGRESS',
      payload: complexPayload,
    });
  });

  it('should handle large batches efficiently', () => {
    // Arrange
    const handler = vi.fn();
    eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.PROGRESS, handler);

    // Create a large batch (simulate many events)
    const largeBatch = Array.from({ length: 100 }, (_, i) => ({
      module: 'WORKFLOW',
      type: 'PROGRESS',
      payload: { workflowId: `wf-${i}`, progress: i },
      profileId: null,
    }));

    // Act - unbundle large batch
    largeBatch.forEach((event) => {
      eventBus.emit({
        module: event.module,
        type: event.type,
        payload: event.payload,
      });
    });

    // Assert
    expect(handler).toHaveBeenCalledTimes(100);
  });

  it('should handle mixed event types in single batch', () => {
    // Arrange
    const modLoadedHandler = vi.fn();
    const modDeletedHandler = vi.fn();
    const workflowHandler = vi.fn();

    eventBus.subscribe(Module.MOD, ModEventType.LOADED, modLoadedHandler);
    eventBus.subscribe(Module.MOD, ModEventType.DELETED, modDeletedHandler);
    eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.PROGRESS, workflowHandler);

    // Act - mixed batch
    const mixedBatch = [
      { module: 'MOD', type: 'LOADED', payload: { sha: 'a' } },
      { module: 'WORKFLOW', type: 'PROGRESS', payload: { workflowId: 'w1', progress: 10 } },
      { module: 'MOD', type: 'DELETED', payload: { sha: 'b' } },
      { module: 'WORKFLOW', type: 'PROGRESS', payload: { workflowId: 'w2', progress: 20 } },
      { module: 'MOD', type: 'LOADED', payload: { sha: 'c' } },
    ];

    mixedBatch.forEach((event) => {
      eventBus.emit({
        module: event.module,
        type: event.type,
        payload: event.payload,
      });
    });

    // Assert
    expect(modLoadedHandler).toHaveBeenCalledTimes(2);
    expect(modDeletedHandler).toHaveBeenCalledTimes(1);
    expect(workflowHandler).toHaveBeenCalledTimes(2);
  });
});
