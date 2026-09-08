import { useState } from 'react';
import { Activity, CheckCircle2, CircleDashed, LockKeyhole, ShieldCheck, Sparkles } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { getAIWorkflows } from '../../services/workflowService';
import type { AIWorkflow } from '../../types/domain';

export function AIWorkflowsPage() {
  const [workflows, setWorkflows] = useState(getAIWorkflows());
  const [selected, setSelected] = useState<AIWorkflow>(workflows[0]);
  const [notice, setNotice] = useState('');
  const completed = selected.steps.filter((step) => step.status === 'Completed').length;
  const progress = Math.round((completed / selected.steps.length) * 100);
  return <div className="page-wrap">
    <div className="page-heading"><div><div className="eyebrow">AI workflow interface</div><h2>Agentic workflow monitor</h2><p>Interface only — the agent orchestrator and model service are intentionally not implemented yet.</p></div><Badge tone="info">Backend pending</Badge></div>
    <div className="ai-banner"><div className="ai-banner-icon"><Sparkles size={20}/></div><div><strong>Safe integration boundary</strong><span>The UI stores no model reasoning. Later, ASP.NET Core can populate workflow state, validation results, approval status and execution summaries through the documented API boundary.</span></div><LockKeyhole size={18}/></div>
    <div className="ai-layout"><Card className="workflow-list"><div className="card-header"><div><div className="eyebrow">Workflows</div><h3>Recent executions</h3></div><Activity size={17}/></div>{workflows.map((workflow)=><button key={workflow.id} className={`workflow-row ${selected.id===workflow.id?'selected':''}`} onClick={()=>setSelected(workflow)}><div><strong>{workflow.id}</strong><span>{workflow.requestId}</span></div><Badge tone={workflow.status==='PendingManagerApproval'?'warning':workflow.status==='Completed'?'success':'info'}>{workflow.status === 'PendingManagerApproval' ? 'Approval' : workflow.status}</Badge></button>)}</Card>
      <Card className="workflow-detail"><div className="proposal-header"><div><div className="eyebrow">{selected.id}</div><h3>Workflow execution summary</h3><p>{selected.objective}</p></div><Badge tone="warning">Manager approval</Badge></div><div className="workflow-progress"><div><strong>{progress}%</strong><span>workflow completion before approval</span></div><div className="mini-bar"><span style={{width:`${progress}%`}}/></div></div><div className="timeline">{selected.steps.map((step, index)=><div className="timeline-row" key={step.id}><div className={`timeline-node ${step.status.toLowerCase()}`}>{step.status === 'Completed' ? <CheckCircle2 size={15}/> : <CircleDashed size={15}/>}</div><div className="timeline-content"><div className="timeline-top"><strong>{index + 1}. {step.name}</strong><Badge tone={step.status==='Completed'?'success':step.status==='Running'?'warning':'neutral'}>{step.status}</Badge></div><p>{step.responsibility}</p></div></div>)}</div><div className="workflow-footer"><ShieldCheck size={17}/><span>Deterministic validation + human approval are shown as first-class UI states, ready for backend integration.</span><Button variant="secondary" onClick={()=>{setNotice('Refresh requested. Backend integration is not connected in this phase.'); setTimeout(()=>setNotice(''), 2500);}}>Refresh status</Button></div>{notice && <div className="notice">{notice}</div>}</Card>
    </div>
    <div className="ai-note"><strong>Implementation boundary</strong><span>No LLM, agent loop, tool calling, prompt execution, or Python service is included in this branch. Only the staff-facing monitor is implemented.</span></div>
  </div>;
}
