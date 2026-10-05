using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using PetCare.Api.Controllers;
using PetCare.Domain.Constants;
using Xunit;

namespace PetCare.Infrastructure.Tests;

/// <summary>
/// Verifies the [Authorize(Roles = ...)] metadata on the workflow endpoints.
/// Role checks are attribute-driven, so these tests reflect over the
/// attributes rather than spinning up a host.
/// </summary>
public class ControllerAuthorizationTests
{
    private static string[] AuthorizedRoles(Type controller, string methodName)
    {
        var method = controller.GetMethod(methodName)
            ?? throw new InvalidOperationException($"{controller.Name}.{methodName} not found");

        var roleString = method
            .GetCustomAttributes<AuthorizeAttribute>()
            .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
            .Select(a => a.Roles!)
            .LastOrDefault() // method-level attribute wins over the class-level one
            ?? throw new InvalidOperationException(
                $"{controller.Name}.{methodName} has no [Authorize(Roles = ...)] attribute");

        return roleString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // POST /api/consultations/{id}/assign — ClinicManager + Admin only.
    [Fact]
    public void AssignConsultation_OnlyManagerAndAdmin()
    {
        var roles = AuthorizedRoles(typeof(ConsultationRequestsController), "Assign");

        Assert.Contains(Roles.ClinicManager, roles);
        Assert.Contains(Roles.SuperAdmin, roles);
        Assert.DoesNotContain(Roles.Veterinarian, roles);
        Assert.DoesNotContain(Roles.PetOwner, roles);
        Assert.DoesNotContain(Roles.InventoryOfficer, roles);
    }

    // POST /api/consultations/follow-up — Veterinarian + Admin only.
    [Fact]
    public void FollowUp_OnlyVeterinarianAndAdmin()
    {
        var roles = AuthorizedRoles(typeof(ConsultationRequestsController), "CreateFollowUp");

        Assert.Contains(Roles.Veterinarian, roles);
        Assert.Contains(Roles.SuperAdmin, roles);
        Assert.DoesNotContain(Roles.PetOwner, roles);
        Assert.DoesNotContain(Roles.ClinicManager, roles);
    }

    // POST /api/prescriptions/{id}/issue — InventoryOfficer + Admin only.
    [Fact]
    public void IssuePrescription_OnlyInventoryOfficerAndAdmin()
    {
        var roles = AuthorizedRoles(typeof(PrescriptionsController), "Issue");

        Assert.Contains(Roles.InventoryOfficer, roles);
        Assert.Contains(Roles.SuperAdmin, roles);
        Assert.DoesNotContain(Roles.Veterinarian, roles);
        Assert.DoesNotContain(Roles.ClinicManager, roles);
        Assert.DoesNotContain(Roles.PetOwner, roles);
    }

    // POST /api/prescriptions/{id}/unavailable — InventoryOfficer + Admin only.
    [Fact]
    public void MarkPrescriptionUnavailable_OnlyInventoryOfficerAndAdmin()
    {
        var roles = AuthorizedRoles(typeof(PrescriptionsController), "MarkUnavailable");

        Assert.Contains(Roles.InventoryOfficer, roles);
        Assert.Contains(Roles.SuperAdmin, roles);
        Assert.DoesNotContain(Roles.Veterinarian, roles);
        Assert.DoesNotContain(Roles.ClinicManager, roles);
        Assert.DoesNotContain(Roles.PetOwner, roles);
    }

    // POST /api/quotations/{id}/mark-paid — InventoryOfficer + Admin only;
    // veterinarians and managers must NOT be able to mark bills paid.
    [Fact]
    public void MarkPaid_OnlyInventoryOfficerAndAdmin()
    {
        var roles = AuthorizedRoles(typeof(QuotationsController), "MarkPaid");

        Assert.Contains(Roles.InventoryOfficer, roles);
        Assert.Contains(Roles.SuperAdmin, roles);
        Assert.DoesNotContain(Roles.Veterinarian, roles);
        Assert.DoesNotContain(Roles.ClinicManager, roles);
        Assert.DoesNotContain(Roles.PetOwner, roles);
    }

    // GET /api/consultations — the general request queue belongs to the
    // requester and clinic management. Veterinarians work from assigned
    // appointments, so they are excluded from consultation reads.
    [Fact]
    public void ConsultationReads_ExcludeVeterinarianAndInventoryOfficer()
    {
        foreach (var method in new[] { "GetAll", "GetByOwnerId", "GetById" })
        {
            var roles = AuthorizedRoles(typeof(ConsultationRequestsController), method);

            Assert.Contains(Roles.PetOwner, roles);
            Assert.Contains(Roles.ClinicManager, roles);
            Assert.Contains(Roles.SuperAdmin, roles);
            Assert.DoesNotContain(Roles.Veterinarian, roles);
            Assert.DoesNotContain(Roles.InventoryOfficer, roles);
        }
    }

    // GET /api/consultations/availability — vets still read availability to
    // pick a slot when filing a follow-up request.
    [Fact]
    public void Availability_IncludesVeterinarian()
    {
        var roles = AuthorizedRoles(typeof(ConsultationRequestsController), "GetAvailability");

        Assert.Contains(Roles.Veterinarian, roles);
        Assert.Contains(Roles.PetOwner, roles);
        Assert.Contains(Roles.ClinicManager, roles);
    }

    // GET /api/quotations/mine — PetOwner.
    [Fact]
    public void MyQuotations_PetOwnerOnly()
    {
        var roles = AuthorizedRoles(typeof(QuotationsController), "GetMyQuotations");

        Assert.Single(roles);
        Assert.Contains(Roles.PetOwner, roles);
    }

    // POST /api/agent-workflows/{id}/approve|reject|revision — the approval
    // decision is the human gate: ClinicManager ONLY. PetOwner and
    // Veterinarian must never reach it.
    [Fact]
    public void AgentWorkflowDecisions_ClinicManagerOnly()
    {
        foreach (var method in new[] { "Approve", "Reject", "RequestRevision" })
        {
            var roles = AuthorizedRoles(typeof(AgentWorkflowsController), method);

            Assert.Single(roles);
            Assert.Contains(Roles.ClinicManager, roles);
            Assert.DoesNotContain(Roles.SuperAdmin, roles);
            Assert.DoesNotContain(Roles.Veterinarian, roles);
            Assert.DoesNotContain(Roles.PetOwner, roles);
        }
    }

    // POST /api/agent-workflows/start|run + history + events —
    // clinic management only.
    [Fact]
    public void AgentWorkflowRun_ExcludesPetOwnerAndVeterinarian()
    {
        foreach (var method in new[] { "Start", "Run", "GetHistory", "RecordEvent" })
        {
            var roles = AuthorizedRoles(typeof(AgentWorkflowsController), method);

            Assert.Contains(Roles.ClinicManager, roles);
            Assert.Contains(Roles.SuperAdmin, roles);
            Assert.DoesNotContain(Roles.PetOwner, roles);
            Assert.DoesNotContain(Roles.Veterinarian, roles);
        }
    }

    // GET /api/agent-workflows/{id} — staff detail view includes vets.
    [Fact]
    public void AgentWorkflowDetail_IncludesVeterinarian()
    {
        var roles = AuthorizedRoles(typeof(AgentWorkflowsController), "Get");

        Assert.Contains(Roles.Veterinarian, roles);
        Assert.Contains(Roles.ClinicManager, roles);
        Assert.Contains(Roles.SuperAdmin, roles);
        Assert.DoesNotContain(Roles.PetOwner, roles);
    }

    // GET /api/agent-workflows/by-consultation/{id} — owners see the reduced
    // status view; the controller branches on IOwnerAccessService.IsPetOwner.
    [Fact]
    public void AgentWorkflowByConsultation_IncludesPetOwner()
    {
        var roles = AuthorizedRoles(typeof(AgentWorkflowsController), "GetByConsultation");

        Assert.Contains(Roles.PetOwner, roles);
        Assert.Contains(Roles.ClinicManager, roles);
        Assert.Contains(Roles.SuperAdmin, roles);
        Assert.DoesNotContain(Roles.Veterinarian, roles);
    }
}
