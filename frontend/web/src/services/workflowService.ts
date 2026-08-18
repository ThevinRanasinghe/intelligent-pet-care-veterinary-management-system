import { workflows } from './mockData';
import type { AIWorkflow } from '../types/domain';

export function getAIWorkflows(): AIWorkflow[] {
  return workflows.map((workflow) => ({
    ...workflow,
    steps: workflow.steps.map((step) => ({ ...step })),
  }));
}
