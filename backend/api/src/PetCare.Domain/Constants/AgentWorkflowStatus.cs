namespace PetCare.Domain.Constants;

/// <summary>
/// Canonical AgentWorkflow lifecycle statuses. These mirror the Python
/// supervisor's state machine — the backend persists whatever the graph
/// reports and only transitions through the values here.
/// </summary>
public static class AgentWorkflowStatus
{
    public const string Created = "Created";
    public const string Planning = "Planning";
    public const string Running = "Running";
    public const string PendingManagerApproval = "PendingManagerApproval";
    public const string Approved = "Approved";
    public const string AwaitingExamination = "AwaitingExamination";
    public const string AwaitingPrescription = "AwaitingPrescription";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";
}

/// <summary>ClinicManager decisions on a pending agent-workflow approval.</summary>
public static class AgentWorkflowDecision
{
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string RevisionRequested = "RevisionRequested";
}

/// <summary>Approval-row lifecycle; Pending rows wait for a manager decision.</summary>
public static class AgentWorkflowApprovalStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string RevisionRequested = "RevisionRequested";
}

/// <summary>Business events asserted into a workflow via the advance path.</summary>
public static class AgentWorkflowEventType
{
    public const string ConsultationSubmitted = "consultation_submitted";
    public const string AppointmentBooked = "appointment_booked";
    public const string ExaminationRecorded = "examination_recorded";
    public const string PrescriptionCreated = "prescription_created";
}
