using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicManagerDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add OrganizationId to Veterinarians
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Veterinarians",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veterinarians_OrganizationId",
                table: "Veterinarians",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Veterinarians_Organizations_OrganizationId",
                table: "Veterinarians",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 2. Add OrganizationId to Appointments
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_OrganizationId",
                table: "Appointments",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Organizations_OrganizationId",
                table: "Appointments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 3. Add OrganizationId to ConsultationRequests
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ConsultationRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_OrganizationId",
                table: "ConsultationRequests",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationRequests_Organizations_OrganizationId",
                table: "ConsultationRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 4. Create AuditLogs table
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId",
                table: "AuditLogs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            // 5. Create AIProposals table
            migrationBuilder.CreateTable(
                name: "AIProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationRequestId = table.Column<string>(type: "character varying(30)", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    Urgency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Routine"),
                    PetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SymptomsSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PreliminaryRecommendation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProposedTreatment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MedicineAvailabilityStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "InStock"),
                    ProposedVeterinarianName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProposedDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProposedTime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuotationTotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    BudgetLimit = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ValidationChecksJson = table.Column<string>(type: "text", nullable: false),
                    ExecutionStepsJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    DecisionNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIProposals_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AIProposals_ConsultationRequests_ConsultationRequestId",
                        column: x => x.ConsultationRequestId,
                        principalTable: "ConsultationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AIProposals_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AIProposals_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIProposals_AppointmentId",
                table: "AIProposals",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AIProposals_ConsultationRequestId",
                table: "AIProposals",
                column: "ConsultationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AIProposals_OrganizationId",
                table: "AIProposals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIProposals_QuotationId",
                table: "AIProposals",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIProposals_Status",
                table: "AIProposals",
                column: "Status");

            // 6. Data backfill: Scope existing records to organizations for test isolation
            // Happy Paws: ef7ea7eb-c571-4a87-b1d9-d9b6b20c2757
            // Greenfield: 1f02abe3-872b-4ad3-a72c-47bb7a1ec0f3
            migrationBuilder.Sql(@"
                UPDATE ""Veterinarians"" SET ""OrganizationId"" = 'ef7ea7eb-c571-4a87-b1d9-d9b6b20c2757' WHERE ""Id"" IN ('11111111-0000-0000-0000-000000000001', '11111111-0000-0000-0000-000000000002');
                UPDATE ""Veterinarians"" SET ""OrganizationId"" = '1f02abe3-872b-4ad3-a72c-47bb7a1ec0f3' WHERE ""Id"" = '11111111-0000-0000-0000-000000000003';
                UPDATE ""Appointments"" SET ""OrganizationId"" = 'ef7ea7eb-c571-4a87-b1d9-d9b6b20c2757' WHERE ""OrganizationId"" IS NULL;
                UPDATE ""ConsultationRequests"" SET ""OrganizationId"" = 'ef7ea7eb-c571-4a87-b1d9-d9b6b20c2757' WHERE ""OrganizationId"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AIProposals");
            migrationBuilder.DropTable(name: "AuditLogs");

            migrationBuilder.DropForeignKey(name: "FK_ConsultationRequests_Organizations_OrganizationId", table: "ConsultationRequests");
            migrationBuilder.DropIndex(name: "IX_ConsultationRequests_OrganizationId", table: "ConsultationRequests");
            migrationBuilder.DropColumn(name: "OrganizationId", table: "ConsultationRequests");

            migrationBuilder.DropForeignKey(name: "FK_Appointments_Organizations_OrganizationId", table: "Appointments");
            migrationBuilder.DropIndex(name: "IX_Appointments_OrganizationId", table: "Appointments");
            migrationBuilder.DropColumn(name: "OrganizationId", table: "Appointments");

            migrationBuilder.DropForeignKey(name: "FK_Veterinarians_Organizations_OrganizationId", table: "Veterinarians");
            migrationBuilder.DropIndex(name: "IX_Veterinarians_OrganizationId", table: "Veterinarians");
            migrationBuilder.DropColumn(name: "OrganizationId", table: "Veterinarians");
        }
    }
}
