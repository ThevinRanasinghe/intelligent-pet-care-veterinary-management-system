using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPetOwnerEmailLowerIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Functional unique index: owner emails unique case-insensitively.
            // Closes the check-then-insert race in GetByEmailAsync-based
            // deduplication — two concurrent registrations can no longer both
            // pass the lookup and insert the same email under different
            // casing. Verified against live data first: zero existing
            // lower("Email") collisions, so this applies cleanly.
            // NOT applied to shared Supabase pending owner approval.
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_PetOwners_Email_Lower""
                  ON ""PetOwners"" (lower(""Email""));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP INDEX ""IX_PetOwners_Email_Lower"";");
        }
    }
}
