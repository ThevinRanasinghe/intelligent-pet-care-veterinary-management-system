using FluentValidation;
using PetCare.Application.DTOs.Approval;

namespace PetCare.Application.Validators;

/// <summary>
/// Structural validation for ApproveRequest: ReviewedBy is required so the
/// CK_Approval_ReviewedBy_Required_When_Decided database constraint is
/// always satisfiable. No reason is required to approve.
/// </summary>
public class ApproveRequestValidator : AbstractValidator<ApproveRequest>
{
    public ApproveRequestValidator()
    {
        RuleFor(r => r.ReviewedBy)
            .NotEmpty()
            .WithMessage("ReviewedBy is required.");
    }
}

/// <summary>
/// Structural validation for RejectRequest: ReviewedBy and a non-empty
/// Reason are both required, per the domain model rule "Reject /
/// RevisionRequested requires a reason" and the
/// CK_Approval_Comment_Required_For_Reject_Or_Revision database constraint.
/// </summary>
public class RejectRequestValidator : AbstractValidator<RejectRequest>
{
    public RejectRequestValidator()
    {
        RuleFor(r => r.ReviewedBy)
            .NotEmpty()
            .WithMessage("ReviewedBy is required.");

        RuleFor(r => r.Reason)
            .NotEmpty()
            .WithMessage("Reason is required when rejecting a quotation.");
    }
}

/// <summary>
/// Structural validation for RequestRevisionRequest: same shape as
/// RejectRequest (ReviewedBy and a non-empty Reason are both required).
/// </summary>
public class RequestRevisionRequestValidator : AbstractValidator<RequestRevisionRequest>
{
    public RequestRevisionRequestValidator()
    {
        RuleFor(r => r.ReviewedBy)
            .NotEmpty()
            .WithMessage("ReviewedBy is required.");

        RuleFor(r => r.Reason)
            .NotEmpty()
            .WithMessage("Reason is required when requesting a revision.");
    }
}
