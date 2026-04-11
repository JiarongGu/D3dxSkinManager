/**
 * Tests for WorkflowService.batchStartModImport
 *
 * The key behaviour under test is that batchStartModImport fires all
 * startModImport calls in parallel (Promise.all) instead of sequentially.
 *
 * Before the fix, workflows were created one-by-one:
 *   for (const path of paths) { await startModImport(...) }
 * With 50 files this took ~1–2 s, leaving a window where "Select All" only
 * captured the workflows already visible and "Cancel All" missed the rest.
 *
 * After the fix, all calls are dispatched simultaneously via Promise.all so
 * the entire list is populated in a single round-trip burst.
 */

import { WorkflowService } from '../workflowService';
import type { WorkflowInfo } from '../../../../modules/workflow/types/workflow.types';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeWorkflow(id: string): WorkflowInfo {
  return {
    id,
    type: 'MOD_IMPORT',
    status: 0,
    context: '{}',
    createdAt: new Date().toISOString(),
  } as WorkflowInfo;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('WorkflowService.batchStartModImport', () => {
  let service: WorkflowService;

  beforeEach(() => {
    service = new WorkflowService();
    jest.clearAllMocks();
  });

  it('returns an empty array when given no paths', async () => {
    const results = await service.batchStartModImport('profile-1', []);
    expect(results).toHaveLength(0);
  });

  it('calls startModImport once per path', async () => {
    const spy = jest
      .spyOn(service, 'startModImport')
      .mockImplementation(async (_, path) => makeWorkflow(path));

    const results = await service.batchStartModImport('p1', ['a', 'b', 'c']);

    expect(spy).toHaveBeenCalledTimes(3);
    expect(spy).toHaveBeenCalledWith('p1', 'a', undefined);
    expect(spy).toHaveBeenCalledWith('p1', 'b', undefined);
    expect(spy).toHaveBeenCalledWith('p1', 'c', undefined);
    expect(results).toHaveLength(3);
  });

  it('forwards the defaultCategory to every startModImport call', async () => {
    const spy = jest
      .spyOn(service, 'startModImport')
      .mockResolvedValue(makeWorkflow('wf-1'));

    await service.batchStartModImport('p1', ['x', 'y'], 'Characters');

    expect(spy).toHaveBeenCalledWith('p1', 'x', 'Characters');
    expect(spy).toHaveBeenCalledWith('p1', 'y', 'Characters');
  });

  it('omits failed paths and returns only successful results', async () => {
    jest
      .spyOn(service, 'startModImport')
      .mockResolvedValueOnce(makeWorkflow('wf-ok-1'))
      .mockRejectedValueOnce(new Error('Import failed'))
      .mockResolvedValueOnce(makeWorkflow('wf-ok-3'));

    const results = await service.batchStartModImport('p1', ['ok-1', 'bad-2', 'ok-3']);

    expect(results).toHaveLength(2);
    expect(results.map(r => r.id)).toEqual(['wf-ok-1', 'wf-ok-3']);
  });

  it('returns an empty array when all paths fail', async () => {
    jest
      .spyOn(service, 'startModImport')
      .mockRejectedValue(new Error('network error'));

    const results = await service.batchStartModImport('p1', ['a', 'b']);
    expect(results).toHaveLength(0);
  });

  it('dispatches all calls in parallel — all are in-flight before any resolves', async () => {
    // Each startModImport returns a promise we control individually.
    const resolvers: Array<(w: WorkflowInfo) => void> = [];

    jest.spyOn(service, 'startModImport').mockImplementation(
      (_, path) =>
        new Promise<WorkflowInfo>(resolve => {
          resolvers.push(resolve);
          // intentionally do NOT resolve here — we want to control ordering
        })
    );

    const batchPromise = service.batchStartModImport('p1', ['a', 'b', 'c']);

    // Flush the microtask queue so Promise.all can start all three calls.
    // With a sequential for-await loop only the first call would be in-flight.
    await Promise.resolve();
    await Promise.resolve(); // two ticks is enough for Promise.all to schedule all

    // All three resolvers must be registered — this is the parallel assertion.
    // With a sequential implementation only the first would be present.
    expect(resolvers).toHaveLength(3);

    // Resolve all and verify results
    resolvers.forEach((resolve, i) => resolve(makeWorkflow(`wf-${i}`)));
    const results = await batchPromise;
    expect(results).toHaveLength(3);
  });
});
