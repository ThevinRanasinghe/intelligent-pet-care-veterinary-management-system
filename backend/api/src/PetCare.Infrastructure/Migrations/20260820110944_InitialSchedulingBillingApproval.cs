using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PetCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchedulingBillingApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Veterinarians",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Specialisation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Veterinarians", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    VeterinarianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Available"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSlots", x => x.Id);
                    table.CheckConstraint("CK_AppointmentSlot_EndTime_After_StartTime", "\"EndTime\" > \"StartTime\"");
                    table.ForeignKey(
                        name: "FK_AppointmentSlots_Veterinarians_VeterinarianId",
                        column: x => x.VeterinarianId,
                        principalTable: "Veterinarians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeterinarianId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Reserved"),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.CheckConstraint("CK_Appointment_EndTime_After_StartTime", "\"EndTime\" > \"StartTime\"");
                    table.ForeignKey(
                        name: "FK_Appointments_AppointmentSlots_AppointmentSlotId",
                        column: x => x.AppointmentSlotId,
                        principalTable: "AppointmentSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Veterinarians_VeterinarianId",
                        column: x => x.VeterinarianId,
                        principalTable: "Veterinarians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Budget = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                    Total = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                    table.CheckConstraint("CK_Quotation_Budget_NonNegative", "\"Budget\" >= 0");
                    table.CheckConstraint("CK_Quotation_Subtotal_NonNegative", "\"Subtotal\" >= 0");
                    table.CheckConstraint("CK_Quotation_Total_NonNegative", "\"Total\" >= 0");
                    table.ForeignKey(
                        name: "FK_Quotations_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                    table.CheckConstraint("CK_Approval_Comment_Required_For_Reject_Or_Revision", "\"Status\" NOT IN ('Rejected', 'RevisionRequested') OR (\"Comment\" IS NOT NULL AND length(btrim(\"Comment\")) > 0)");
                    table.CheckConstraint("CK_Approval_ReviewedBy_Required_When_Decided", "\"Status\" = 'Pending' OR \"ReviewedBy\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Approvals_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuotationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationItems", x => x.Id);
                    table.CheckConstraint("CK_QuotationItem_Category_Allowed", "\"Category\" IN ('Consultation', 'Examination', 'Treatment', 'Medicine', 'Other')");
                    table.CheckConstraint("CK_QuotationItem_Quantity_Positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_QuotationItem_TotalPrice_Equals_Quantity_Times_UnitPrice", "\"TotalPrice\" = \"Quantity\" * \"UnitPrice\"");
                    table.CheckConstraint("CK_QuotationItem_TotalPrice_NonNegative", "\"TotalPrice\" >= 0");
                    table.CheckConstraint("CK_QuotationItem_UnitPrice_NonNegative", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_QuotationItems_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ApprovalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalHistories_Approvals_ApprovalId",
                        column: x => x.ApprovalId,
                        principalTable: "Approvals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Veterinarians",
                columns: new[] { "Id", "Active", "Branch", "CreatedAt", "Name", "Specialisation", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), true, "Colombo", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dr. Anika Perera", "Small Animal Medicine", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("11111111-0000-0000-0000-000000000002"), true, "Kandy", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dr. Rohan Fernando", "Surgery", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("11111111-0000-0000-0000-000000000003"), true, "Galle", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dr. Nadee Silva", "Dermatology", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "AppointmentSlots",
                columns: new[] { "Id", "Branch", "CreatedAt", "Date", "EndTime", "StartTime", "UpdatedAt", "VeterinarianId" },
                values: new object[,]
                {
                    { new Guid("22222222-0000-0000-0000-000000000001"), "Colombo", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2025, 6, 2), new TimeOnly(9, 30, 0), new TimeOnly(9, 0, 0), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-0000-0000-0000-000000000001") },
                    { new Guid("22222222-0000-0000-0000-000000000002"), "Colombo", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2025, 6, 2), new TimeOnly(10, 30, 0), new TimeOnly(10, 0, 0), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-0000-0000-0000-000000000001") },
                    { new Guid("22222222-0000-0000-0000-000000000003"), "Kandy", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2025, 6, 2), new TimeOnly(11, 30, 0), new TimeOnly(11, 0, 0), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-0000-0000-0000-000000000002") },
                    { new Guid("22222222-0000-0000-0000-000000000004"), "Galle", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2025, 6, 2), new TimeOnly(14, 30, 0), new TimeOnly(14, 0, 0), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("11111111-0000-0000-0000-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_PetId",
                table: "Appointments",
                column: "PetId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_ScheduledStart",
                table: "Appointments",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_Status",
                table: "Appointments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_VeterinarianId",
                table: "Appointments",
                column: "VeterinarianId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_VeterinarianId_Date",
                table: "Appointments",
                columns: new[] { "VeterinarianId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentSlotId",
                table: "Appointments",
                column: "AppointmentSlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSlot_StartTime",
                table: "AppointmentSlots",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSlot_VeterinarianId_Date",
                table: "AppointmentSlots",
                columns: new[] { "VeterinarianId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSlots_VeterinarianId_Date_StartTime",
                table: "AppointmentSlots",
                columns: new[] { "VeterinarianId", "Date", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalHistory_ApprovalId_ChangedAt",
                table: "ApprovalHistories",
                columns: new[] { "ApprovalId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Approval_Status",
                table: "Approvals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_QuotationId",
                table: "Approvals",
                column: "QuotationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItem_QuotationId",
                table: "QuotationItems",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotation_Status",
                table: "Quotations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_AppointmentId",
                table: "Quotations",
                column: "AppointmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalHistories");

            migrationBuilder.DropTable(
                name: "QuotationItems");

            migrationBuilder.DropTable(
                name: "Approvals");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "AppointmentSlots");

            migrationBuilder.DropTable(
                name: "Veterinarians");
        }
    }
}
