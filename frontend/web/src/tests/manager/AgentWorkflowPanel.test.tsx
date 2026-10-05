import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AgentWorkflowPanel } from '../../features/manager/AgentWorkflowPanel';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const baseWorkflow = {
  id: 'wf-1',
  consultationRequestId: 'CON-1',
  organizationId: 'org-1',
  objective: 'Plan and schedule consultation CON-1',
  status: 'Created',
  currentStep: 0,
  plan: null,
  proposal: null,
  approvedAction: null,
  delegationCount: 0,
  revisionCount: 0,
  failureReason: null,
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
  completedAt: null,
  steps: [],
  approvals: [],
};

const pendingWorkflow = {
  ...baseWorkflow,
  status: 'PendingManagerApproval',
  currentStep: 2,
  proposal: {
    veterinarianId: 'vet-1',
    veterinarianName: 'Dr. Silva',
    date: '2026-10-10',
    startTime: '10:00',
    endTime: '11:00',
    branch: 'Colombo',
    quotationProposal: {
      budget: 5000,
      estimatedTotal: 4500,
      withinBudget: true,
      items: [{ description: 'Consultation', quantity: 1, unitPrice: 3500 }],
    },
    validationSummary: { slotFound: true, withinBudget: true },
    confidence: 'High',
    planningNotes: 'Earliest suitable slot.',
    disclaimer: 'AI-generated proposal — manager approval required.',
  },
  approvals: [
    {
      id: 'ap-1',
      stepNumber: 2,
      status: 'Pending',
      proposal: {},
      decidedByUserId: null,
      decidedAt: null,
      comments: null,
      createdAt: '2026-10-01T01:00:00Z',
    },
  ],
};

const historyPayload = {
  workflow: {
    ...baseWorkflow,
    status: 'PendingManagerApproval',
    plan: {
      steps: [
        { stepNumber: 1, type: 'agent', agent: 'consultation_agent', purpose: 'Triage', requires: 'consultation_submitted' },
        { stepNumber: 2, type: 'agent', agent: 'scheduling_agent', purpose: 'Propose slot', requires: 'consultation_agent' },
        { stepNumber: 3, type: 'human_approval', purpose: 'Manager decision' },
      ],
    },
  },
  steps: [
    {
      id: 'st-1',
      stepNumber: 1,
      agentName: 'consultation_agent',
      task: 'triage',
      status: 'Completed',
      retryCount: 0,
      startedAt: '2026-10-01T00:01:00Z',
      completedAt: '2026-10-01T00:01:05Z',
      toolCalls: [{ tool: 'get_consultation', ok: true, durationMs: 42 }],
    },
  ],
  approvals: [
    {
      id: 'ap-1',
      stepNumber: 2,
      status: 'Pending',
      decidedByUserId: null,
      decidedAt: null,
      comments: null,
      createdAt: '2026-10-01T01:00:00Z',
    },
  ],
  events: [
    { seq: 1, timestamp: '2026-10-01T00:01:00Z', node: 'supervisor_plan', agentName: null, eventType: 'plan_created' },
    { seq: 2, timestamp: '2026-10-01T00:02:00Z', node: 'approval_gate', agentName: null, eventType: 'awaiting_decision' },
  ],
};

function stubFetch(workflowBody: unknown, workflowStatus = 200) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes('/agent-workflows/by-consultation/')) {
      return jsonResponse(workflowBody, workflowStatus >= 200 && workflowStatus < 300, workflowStatus);
    }
    if (url.includes('/history')) return jsonResponse(historyPayload);
    return jsonResponse(baseWorkflow);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('AgentWorkflowPanel', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('renders the workflow status and offers Run for a Created workflow', async () => {
    stubFetch(baseWorkflow);
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    expect(await screen.findByTestId('workflow-status')).toHaveTextContent('Created');
    expect(screen.getByRole('button', { name: 'Run AI workflow' })).toBeInTheDocument();
  });

  it('runs the workflow and shows the returned pending proposal', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes('/run')) return jsonResponse(pendingWorkflow);
      if (url.includes('/by-consultation/')) return jsonResponse(baseWorkflow);
      return jsonResponse(baseWorkflow);
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    await userEvent.click(await screen.findByRole('button', { name: 'Run AI workflow' }));

    expect(await screen.findByTestId('workflow-status')).toHaveTextContent('PendingManagerApproval');
    expect(screen.getByText(/Dr\. Silva/)).toBeInTheDocument();
    expect(screen.getByText(/4500/)).toBeInTheDocument();
    expect(screen.getByText(/manager approval required/i)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/agent-workflows/wf-1/run'))).toBe(true);
  });

  it('approves with an optional comment and shows the booked state', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith('/approve')) {
        return jsonResponse({ ...baseWorkflow, status: 'AwaitingExamination' });
      }
      if (url.includes('/by-consultation/')) return jsonResponse(pendingWorkflow);
      return jsonResponse(baseWorkflow);
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    await userEvent.click(await screen.findByRole('button', { name: 'Approve' }));

    expect(await screen.findByTestId('workflow-status')).toHaveTextContent('AwaitingExamination');
    expect(screen.getByText('Appointment booked from approved proposal.')).toBeInTheDocument();
    const approveCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/approve'));
    expect(approveCall).toBeTruthy();
    expect((approveCall![1] as RequestInit).method).toBe('POST');
  });

  it('blocks reject without a comment and does NOT call the API', async () => {
    const fetchMock = stubFetch(pendingWorkflow);
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    await userEvent.click(await screen.findByRole('button', { name: 'Reject' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/comment is required/i);
    expect(
      fetchMock.mock.calls.filter(([url]) => String(url).endsWith('/reject')),
    ).toHaveLength(0);
  });

  it('rejects with a comment', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      void init;
      if (url.endsWith('/reject')) return jsonResponse({ ...baseWorkflow, status: 'Rejected' });
      if (url.includes('/by-consultation/')) return jsonResponse(pendingWorkflow);
      return jsonResponse(baseWorkflow);
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    await userEvent.type(
      await screen.findByLabelText('Decision comment'),
      'Owner wants a different clinic',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Reject' }));

    expect(await screen.findByTestId('workflow-status')).toHaveTextContent('Rejected');
    const rejectCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/reject'));
    expect(JSON.parse((rejectCall![1] as RequestInit).body as string)).toEqual({
      comments: 'Owner wants a different clinic',
    });
  });

  it('renders execution history tables', async () => {
    stubFetch({ ...baseWorkflow, status: 'PendingManagerApproval' });
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    await userEvent.click(await screen.findByRole('button', { name: 'View execution history' }));

    expect(await screen.findByText('Plan')).toBeInTheDocument();
    expect(screen.getAllByText('consultation_agent').length).toBeGreaterThan(0);
    expect(screen.getByText('Agent steps')).toBeInTheDocument();
    expect(screen.getByText(/get_consultation — ✓ ok \(42ms\)/)).toBeInTheDocument();
    expect(screen.getByText('Trajectory events')).toBeInTheDocument();
    // Friendly label plus the raw event code shown beneath it.
    expect(screen.getByText('Plan created')).toBeInTheDocument();
    expect(screen.getAllByText('plan_created').length).toBeGreaterThan(0);
    expect(screen.getByText('Waiting for manager decision')).toBeInTheDocument();
  });

  it('shows a non-blocking message when the service fails', async () => {
    stubFetch({ detail: 'boom' }, 500);
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/workflow action failed/i);
  });

  it('renders the 10-stage workflow progress derived from persisted state', async () => {
    stubFetch({
      ...pendingWorkflow,
      steps: [
        {
          id: 'st-1', stepNumber: 1, agentName: 'consultation_agent',
          task: 'triage', status: 'Completed', retryCount: 0,
          startedAt: '2026-10-01T00:01:00Z',
        },
        {
          id: 'st-2', stepNumber: 2, agentName: 'scheduling_agent',
          task: 'schedule', status: 'Completed', retryCount: 0,
          startedAt: '2026-10-01T00:02:00Z',
        },
      ],
    });
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    const progress = await screen.findByTestId('workflow-progress');
    expect(progress).toHaveTextContent('Consultation submitted');
    expect(progress).toHaveTextContent('Scheduling proposal');
    expect(progress).toHaveTextContent('Manager approval');
    expect(progress).toHaveTextContent('Appointment booking');
    expect(progress).toHaveTextContent('Completed');
    // Approval is the active stage while pending.
    expect(
      progress.querySelector('[aria-current="step"]'),
    ).toHaveTextContent('Manager approval');
  });

  it('shows the consultation assessment and owner request sections', async () => {
    const wf = {
      ...pendingWorkflow,
      steps: [
        {
          id: 'st-1',
          stepNumber: 1,
          agentName: 'consultation_agent',
          task: 'triage',
          status: 'Completed',
          retryCount: 0,
          startedAt: '2026-10-01T00:01:00Z',
          completedAt: '2026-10-01T00:01:05Z',
          output: {
            priority: 'High',
            complexity: 'complex',
            requiredSlots: 2,
            estimatedDurationMinutes: 120,
            recommendedChecks: ['Orthopedic examination'],
            schedulingReason: 'Symptoms may require an extended examination.',
            confidence: 0.8,
          },
        },
      ],
      proposal: {
        appointment: {
          veterinarianId: 'vet-1',
          veterinarianName: 'Dr. Silva',
          date: '2026-10-10',
          startTime: '12:00',
          endTime: '14:00',
          slotIds: ['s1', 's2'],
          slotCount: 2,
          requiredSlots: 2,
          estimatedDurationMinutes: 120,
          usedPreferredTime: false,
          usedPreferredDate: true,
          fallbackType: 'same_day_nearest_time',
          requestedDate: '2026-10-10',
          requestedTime: '10:00',
        },
      },
    };
    stubFetch(wf);
    render(
      <AgentWorkflowPanel
        consultationId="CON-1"
        canDecide
        request={{
          id: 'CON-1',
          petName: 'Rocky',
          ownerName: 'Nimal',
          symptoms: 'Hit by a vehicle, limping badly',
          preferredDate: '2026-10-10',
          preferredTime: '10:00',
          urgency: 'High',
        }}
      />,
    );

    // Owner request
    expect(await screen.findByText(/Owner request/)).toBeInTheDocument();
    expect(screen.getByText(/Rocky/)).toBeInTheDocument();
    expect(screen.getByText(/limping badly/)).toBeInTheDocument();

    // Consultation assessment
    const assessment = screen.getByTestId('consultation-assessment');
    expect(assessment).toHaveTextContent('Complexity: Complex');
    expect(assessment).toHaveTextContent('Estimated appointment time: 2 hours');
    expect(assessment).toHaveTextContent('Required slots: 2');
    expect(assessment).toHaveTextContent('Orthopedic examination');
    expect(assessment).toHaveTextContent('Confidence: 80%');

    // Scheduling proposal — the "why" the manager needs
    const proposal = screen.getByTestId('scheduling-proposal');
    expect(proposal).toHaveTextContent('Requested: October 10, 2026, 10:00');
    expect(proposal).toHaveTextContent('12:00 – 14:00');
    expect(proposal).toHaveTextContent('Required slots: 2');
    expect(proposal).toHaveTextContent('Preferred time available: No');
    expect(proposal).toHaveTextContent('Same-day nearest available time');
    expect(proposal).toHaveTextContent(/nearest suitable consecutive slots/i);

    // Specialist agents are individually identified
    const agents = screen.getByTestId('specialist-agents');
    expect(agents).toHaveTextContent('Consultation Agent');
    expect(agents).toHaveTextContent('Consultation assessment');
    expect(agents).toHaveTextContent('Completed');
  });

  it('shows a friendly safe-failure message for no_valid_slot', async () => {
    stubFetch({ ...baseWorkflow, status: 'Failed', failureReason: 'no_valid_slot' });
    render(<AgentWorkflowPanel consultationId="CON-1" canDecide />);

    expect(
      await screen.findByText(/No suitable appointment window/i),
    ).toBeInTheDocument();
  });
});
