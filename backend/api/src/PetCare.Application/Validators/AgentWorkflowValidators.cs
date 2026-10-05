using FluentValidation;
using PetCare.Application.DTOs.Agentic.Workflows;

namespace PetCare.Application.Validators;

/// <summary>
/// A rejection or revision request must carry a reason — the comment is
/// surfaced to the owner/staff reviewing the workflow.
/// </summary>
public class WorkflowDecisionWithCommentsValidator : AbstractValidator<WorkflowDecisionRequest>
{
    public WorkflowDecisionWithCommentsValidator()
    {
        RuleFor(r => r.Comments)
            .NotEmpty().WithMessage("Comments are required for this decision.")
            .MaximumLength(1000);
    }
}

/// <summary>Optional comments on an approval (still bounded).</summary>
public class WorkflowDecisionRequestValidator : AbstractValidator<WorkflowDecisionRequest>
{
    public WorkflowDecisionRequestValidator()
    {
        RuleFor(r => r.Comments)
            .MaximumLength(1000);
    }
}

public class WorkflowEventRequestValidator : AbstractValidator<WorkflowEventRequest>
{
    public WorkflowEventRequestValidator()
    {
        RuleFor(r => r.EventType)
            .NotEmpty().WithMessage("EventType is required.")
            .MaximumLength(60);

        RuleFor(r => r.ReferenceId)
            .NotEmpty().WithMessage("ReferenceId is required.")
            .MaximumLength(64);
    }
}
