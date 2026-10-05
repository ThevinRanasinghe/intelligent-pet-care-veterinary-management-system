using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class AgentWorkflowConfiguration : IEntityTypeConfiguration<AgentWorkflow>
{
    public void Configure(EntityTypeBuilder<AgentWorkflow> builder)
    {
        builder.ToTable("AgentWorkflows");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConsultationRequestId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Objective)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.PlanJson).HasColumnType("text");
        builder.Property(x => x.ProposalJson).HasColumnType("text");
        builder.Property(x => x.ApprovedActionJson).HasColumnType("text");

        builder.Property(x => x.FailureReason)
            .HasMaxLength(300);

        // One workflow per consultation request.
        builder.HasIndex(x => x.ConsultationRequestId)
            .IsUnique()
            .HasDatabaseName("UX_AgentWorkflows_ConsultationRequestId");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_AgentWorkflows_Status");

        builder.HasIndex(x => x.OrganizationId)
            .HasDatabaseName("IX_AgentWorkflows_OrganizationId");

        builder.HasOne(x => x.ConsultationRequest)
            .WithMany()
            .HasForeignKey(x => x.ConsultationRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Steps)
            .WithOne(x => x.Workflow)
            .HasForeignKey(x => x.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Approvals)
            .WithOne(x => x.Workflow)
            .HasForeignKey(x => x.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Events)
            .WithOne(x => x.Workflow)
            .HasForeignKey(x => x.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgentWorkflowStepConfiguration : IEntityTypeConfiguration<AgentWorkflowStep>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowStep> builder)
    {
        builder.ToTable("AgentWorkflowSteps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AgentName).HasMaxLength(60);
        builder.Property(x => x.Task).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(500);

        builder.Property(x => x.InputSummaryJson).HasColumnType("text");
        builder.Property(x => x.OutputJson).HasColumnType("text");
        builder.Property(x => x.ToolCallsJson).HasColumnType("text");
        builder.Property(x => x.ValidationJson).HasColumnType("text");

        builder.HasIndex(x => x.WorkflowId)
            .HasDatabaseName("IX_AgentWorkflowSteps_WorkflowId");
    }
}

public class AgentWorkflowApprovalConfiguration : IEntityTypeConfiguration<AgentWorkflowApproval>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowApproval> builder)
    {
        builder.ToTable("AgentWorkflowApprovals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ProposalJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.Comments).HasMaxLength(1000);

        builder.HasIndex(x => x.WorkflowId)
            .HasDatabaseName("IX_AgentWorkflowApprovals_WorkflowId");
    }
}

public class AgentWorkflowEventConfiguration : IEntityTypeConfiguration<AgentWorkflowEvent>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowEvent> builder)
    {
        builder.ToTable("AgentWorkflowEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Node).HasMaxLength(60).IsRequired();
        builder.Property(x => x.AgentName).HasMaxLength(60);
        builder.Property(x => x.EventType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.DetailJson).HasColumnType("text");

        builder.HasIndex(x => x.WorkflowId)
            .HasDatabaseName("IX_AgentWorkflowEvents_WorkflowId");
    }
}
