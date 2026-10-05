import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../services/api';
import {
  approve,
  getByConsultation,
  history,
  reject,
  requestRevision,
  run,
  start,
} from '../../services/agentWorkflowService';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  } as Response;
}

const workflow = {
  id: 'wf-1',
  consultationRequestId: 'CON-1',
  status: 'Created',
  steps: [],
  approvals: [],
};

describe('agentWorkflowService API integration', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('GETs the workflow for a consultation', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(workflow));

    const result = await getByConsultation('CON-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/agent-workflows/by-consultation/CON-1'),
      expect.any(Object),
    );
    expect(result?.id).toBe('wf-1');
  });

  it('returns null on 404 for a consultation with no workflow', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ detail: 'not found' }, false, 404));

    await expect(getByConsultation('CON-9')).resolves.toBeNull();
  });

  it('POSTs start with the consultation id', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(workflow));

    await start('CON-1');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/agent-workflows/start');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toEqual({ consultationRequestId: 'CON-1' });
  });

  it('POSTs run for a workflow', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(workflow));

    await run('wf-1');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/agent-workflows/wf-1/run');
    expect(init.method).toBe('POST');
  });

  it('POSTs approve with optional comments', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ ...workflow, status: 'AwaitingExamination' }));

    await approve('wf-1', 'Looks good');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/agent-workflows/wf-1/approve');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toEqual({ comments: 'Looks good' });
  });

  it('POSTs reject and revision with comments', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ ...workflow, status: 'Rejected' }));
    await reject('wf-1', 'Not suitable');
    expect(fetchMock.mock.calls[0][0]).toContain('/agent-workflows/wf-1/reject');
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ comments: 'Not suitable' });

    fetchMock.mockResolvedValueOnce(jsonResponse(workflow));
    await requestRevision('wf-1', 'Later slot please');
    expect(fetchMock.mock.calls[1][0]).toContain('/agent-workflows/wf-1/revision');
    expect(JSON.parse(fetchMock.mock.calls[1][1].body as string)).toEqual({ comments: 'Later slot please' });
  });

  it('GETs execution history', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({ workflow, steps: [], approvals: [], events: [] }),
    );

    const result = await history('wf-1');

    expect(fetchMock.mock.calls[0][0]).toContain('/agent-workflows/wf-1/history');
    expect(result.workflow.id).toBe('wf-1');
  });

  it('throws ApiError on a 409 conflict', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ detail: 'awaiting decision' }, false, 409),
    );

    await expect(run('wf-1')).rejects.toBeInstanceOf(ApiError);
    await expect(run('wf-1')).rejects.toMatchObject({ status: 409 });
  });
});
