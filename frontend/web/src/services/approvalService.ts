import type { ApprovalProposal } from '../types/domain';
import { approvalProposals } from './mockData';

export function getApprovalProposals(): ApprovalProposal[] {
  return approvalProposals.map((proposal) => ({
    ...proposal,
    validationChecks: proposal.validationChecks.map((check) => ({ ...check })),
  }));
}
