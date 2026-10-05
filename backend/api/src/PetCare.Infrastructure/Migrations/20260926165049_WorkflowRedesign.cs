using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Veterinarians",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "Quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByUserId",
                table: "Quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Quotations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                table: "Prescriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Prescriptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessedAt",
                table: "Prescriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedByUserId",
                table: "Prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Prescriptions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RequestStatus",
                table: "Prescriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "Prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnavailableReason",
                table: "Prescriptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "Examinations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VeterinarianCharge",
                table: "Examinations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RequestType",
                table: "ConsultationRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Initial");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByVeterinarianId",
                table: "ConsultationRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsultationRequestId",
                table: "Appointments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Appointments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Initial");

            migrationBuilder.CreateIndex(
                name: "IX_Veterinarians_UserId",
                table: "Veterinarians",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Quotation_PaidByUserId",
                table: "Quotations",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ProcessedByUserId",
                table: "Prescriptions",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_RequestStatus",
                table: "Prescriptions",
                column: "RequestStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ReservationId",
                table: "Prescriptions",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Examinations_AppointmentId",
                table: "Examinations",
                column: "AppointmentId",
                unique: true,
                filter: "\"AppointmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_RequestedByVeterinarianId",
                table: "ConsultationRequests",
                column: "RequestedByVeterinarianId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConsultationRequest_RequestType_Allowed",
                table: "ConsultationRequests",
                sql: "\"RequestType\" IN ('Initial', 'FollowUp')");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_ConsultationRequestId",
                table: "Appointments",
                column: "ConsultationRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_ConsultationRequests_ConsultationRequestId",
                table: "Appointments",
                column: "ConsultationRequestId",
                principalTable: "ConsultationRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationRequests_Veterinarians_RequestedByVeterinarianId",
                table: "ConsultationRequests",
                column: "RequestedByVeterinarianId",
                principalTable: "Veterinarians",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Examinations_Appointments_AppointmentId",
                table: "Examinations",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_MedicineReservations_ReservationId",
                table: "Prescriptions",
                column: "ReservationId",
                principalTable: "MedicineReservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_Users_ProcessedByUserId",
                table: "Prescriptions",
                column: "ProcessedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Users_PaidByUserId",
                table: "Quotations",
                column: "PaidByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Veterinarians_Users_UserId",
                table: "Veterinarians",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_ConsultationRequests_ConsultationRequestId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationRequests_Veterinarians_RequestedByVeterinarianId",
                table: "ConsultationRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Examinations_Appointments_AppointmentId",
                table: "Examinations");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_MedicineReservations_ReservationId",
                table: "Prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_Users_ProcessedByUserId",
                table: "Prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Users_PaidByUserId",
                table: "Quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_Veterinarians_Users_UserId",
                table: "Veterinarians");

            migrationBuilder.DropIndex(
                name: "IX_Veterinarians_UserId",
                table: "Veterinarians");

            migrationBuilder.DropIndex(
                name: "IX_Quotation_PaidByUserId",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_ProcessedByUserId",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_RequestStatus",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_ReservationId",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Examinations_AppointmentId",
                table: "Examinations");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationRequests_RequestedByVeterinarianId",
                table: "ConsultationRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConsultationRequest_RequestType_Allowed",
                table: "ConsultationRequests");

            migrationBuilder.DropIndex(
                name: "IX_Appointment_ConsultationRequestId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Veterinarians");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "PaidByUserId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "ProcessedByUserId",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "RequestStatus",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "UnavailableReason",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "Examinations");

            migrationBuilder.DropColumn(
                name: "VeterinarianCharge",
                table: "Examinations");

            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "RequestedByVeterinarianId",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "ConsultationRequestId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Appointments");
        }
    }
}
