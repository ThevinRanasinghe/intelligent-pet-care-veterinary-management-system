using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationRequests_Organizations_OrganizationId",
                table: "ConsultationRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationRequests_OrganizationId",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ConsultationRequests");
        }
    }
}
