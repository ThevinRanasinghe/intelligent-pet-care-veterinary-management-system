using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationRequestSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ConsultationRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Online");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConsultationRequest_Source_Allowed",
                table: "ConsultationRequests",
                sql: "\"Source\" IN ('Online', 'WalkIn')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConsultationRequest_Source_Allowed",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ConsultationRequests");
        }
    }
}
