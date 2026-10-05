using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

/// <summary>
/// InMemory persistence tests for AgentWorkflowRepository: create/update,
/// child collections, and reload through a NEW DbContext (request boundary).
/// The unique ConsultationRequestId index itself is database-level — it is
/// defined in AgentWorkflowConfiguration and verified via the migration;
/// here we exercise the idempotent service-level path that relies on it.
/// </summary>
public class AgentWorkflowPersistenceTests
{
    private static DbContextOptions<PetCareDbContext> InMemoryOptions(string name)
        => new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

    private static AgentWorkflowRepository RepoFor(PetCareDbContext db)
        => new(db, TestTenantContext.Unscoped);

    private static AgentWorkflow NewWorkflow(string consultationId = "CR-1") => new()
    {
        ConsultationRequestId = consultationId,
        OrganizationId = Guid.NewGuid(),
        Objective = "Plan and schedule consultation",
        Status = AgentWorkflowStatus.Created
    };

    [Fact]
    public async Task Create_and_reload_in_new_context()
    {
        var options = InMemoryOptions(Guid.NewGuid().ToString());
        var workflow = NewWorkflow();

        await using (var db = new PetCareDbContext(options))
        {
            await RepoFor(db).AddAsync(workflow);
            await db.SaveChangesAsync();
        }

        await using (var db2 = new PetCareDbContext(options))
        {
            var loaded = await RepoFor(db2).GetByIdAsync(workflow.Id);
            Assert.NotNull(loaded);
            Assert.Equal("CR-1", loaded!.ConsultationRequestId);
            Assert.Equal(AgentWorkflowStatus.Created, loaded.Status);
        }
    }

    [Fact]
    public async Task Update_persists_status_and_plan()
    {
        var options = InMemoryOptions(Guid.NewGuid().ToString());
        var workflow = NewWorkflow();

        await using (var db = new PetCareDbContext(options))
        {
            await RepoFor(db).AddAsync(workflow);
            workflow.Status = AgentWorkflowStatus.PendingManagerApproval;
            workflow.PlanJson = "{\"steps\":[]}";
            await db.SaveChangesAsync();
        }

        await using (var db2 = new PetCareDbContext(options))
        {
            var loaded = await RepoFor(db2).GetByIdAsync(workflow.Id);
            Assert.Equal(AgentWorkflowStatus.PendingManagerApproval, loaded!.Status);
            Assert.Equal("{\"steps\":[]}", loaded.PlanJson);
        }
    }

    [Fact]
    public async Task Steps_and_events_survive_reload()
    {
        var options = InMemoryOptions(Guid.NewGuid().ToString());
        var workflow = NewWorkflow();
        workflow.Steps.Add(new AgentWorkflowStep
        {
            StepNumber = 1,
            AgentName = "scheduling_agent",
            Task = "propose",
            Status = "Completed",
            StartedAt = DateTime.UtcNow
        });
        workflow.Events.Add(new AgentWorkflowEvent
        {
            Seq = 1,
            Node = "supervisor_plan",
            EventType = "plan_ready",
            Timestamp = DateTime.UtcNow
        });

        await using (var db = new PetCareDbContext(options))
        {
            await RepoFor(db).AddAsync(workflow);
            await db.SaveChangesAsync();
        }

        await using (var db2 = new PetCareDbContext(options))
        {
            var loaded = await RepoFor(db2).GetWithDetailsAsync(workflow.Id);
            Assert.Single(loaded!.Steps);
            Assert.Single(loaded.Events);
            Assert.Equal("scheduling_agent", loaded.Steps.First().AgentName);
            Assert.Equal(1, loaded.Events.First().Seq);
        }
    }

    [Fact]
    public async Task Approval_persists_and_loads_with_workflow()
    {
        var options = InMemoryOptions(Guid.NewGuid().ToString());
        var workflow = NewWorkflow();
        workflow.Approvals.Add(new AgentWorkflowApproval
        {
            StepNumber = 2,
            Status = AgentWorkflowApprovalStatus.Pending,
            ProposalJson = "{\"veterinarianId\":\"V-1\"}",
            CreatedAt = DateTime.UtcNow
        });

        await using (var db = new PetCareDbContext(options))
        {
            await RepoFor(db).AddAsync(workflow);
            await db.SaveChangesAsync();
        }

        await using (var db2 = new PetCareDbContext(options))
        {
            var loaded = await RepoFor(db2).GetWithDetailsAsync(workflow.Id);
            var approval = Assert.Single(loaded!.Approvals);
            Assert.Equal(AgentWorkflowApprovalStatus.Pending, approval.Status);
        }
    }

    [Fact]
    public async Task GetByConsultationId_resolves_persisted_workflow()
    {
        var options = InMemoryOptions(Guid.NewGuid().ToString());
        var workflow = NewWorkflow("CR-UNIQUE-1");

        await using (var db = new PetCareDbContext(options))
        {
            await RepoFor(db).AddAsync(workflow);
            await db.SaveChangesAsync();
        }

        // Reload in a new context — this is the idempotency lookup the
        // service performs before inserting (backed by the DB unique index).
        await using var db2 = new PetCareDbContext(options);
        var loaded = await RepoFor(db2).GetByConsultationIdAsync("CR-UNIQUE-1");
        Assert.NotNull(loaded);
        Assert.Equal(workflow.Id, loaded!.Id);
    }

    [Fact]
    public async Task GetStatusesAsync_returns_one_status_per_consultation()
    {
        var options = InMemoryOptions(Guid.NewGuid().ToString());
        var a = NewWorkflow("CR-A");
        var b = NewWorkflow("CR-B");
        b.Status = AgentWorkflowStatus.PendingManagerApproval;

        await using var db = new PetCareDbContext(options);
        var repo = RepoFor(db);
        await repo.AddAsync(a);
        await repo.AddAsync(b);
        await db.SaveChangesAsync();

        var statuses = await repo.GetStatusesAsync(new[] { "CR-A", "CR-B", "CR-MISSING" });

        Assert.Equal(2, statuses.Count);
        Assert.Equal(AgentWorkflowStatus.Created, statuses["CR-A"]);
        Assert.Equal(AgentWorkflowStatus.PendingManagerApproval, statuses["CR-B"]);
    }
}
