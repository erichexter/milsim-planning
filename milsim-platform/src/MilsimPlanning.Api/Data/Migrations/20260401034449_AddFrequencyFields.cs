using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequencyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Squads"" ADD COLUMN IF NOT EXISTS ""BackupFrequency"" text NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""Squads"" ADD COLUMN IF NOT EXISTS ""PrimaryFrequency"" text NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""Platoons"" ADD COLUMN IF NOT EXISTS ""BackupFrequency"" text NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""Platoons"" ADD COLUMN IF NOT EXISTS ""PrimaryFrequency"" text NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""Factions"" ADD COLUMN IF NOT EXISTS ""CommandBackupFrequency"" text NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""Factions"" ADD COLUMN IF NOT EXISTS ""CommandPrimaryFrequency"" text NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackupFrequency",
                table: "Squads");

            migrationBuilder.DropColumn(
                name: "PrimaryFrequency",
                table: "Squads");

            migrationBuilder.DropColumn(
                name: "BackupFrequency",
                table: "Platoons");

            migrationBuilder.DropColumn(
                name: "PrimaryFrequency",
                table: "Platoons");

            migrationBuilder.DropColumn(
                name: "CommandBackupFrequency",
                table: "Factions");

            migrationBuilder.DropColumn(
                name: "CommandPrimaryFrequency",
                table: "Factions");
        }
    }
}
