using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequencyFieldsToHierarchyEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SquadBackupFrequency",
                table: "Squads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SquadPrimaryFrequency",
                table: "Squads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatoonBackupFrequency",
                table: "Platoons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatoonPrimaryFrequency",
                table: "Platoons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommandBackupFrequency",
                table: "Factions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommandPrimaryFrequency",
                table: "Factions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SquadBackupFrequency",
                table: "Squads");

            migrationBuilder.DropColumn(
                name: "SquadPrimaryFrequency",
                table: "Squads");

            migrationBuilder.DropColumn(
                name: "PlatoonBackupFrequency",
                table: "Platoons");

            migrationBuilder.DropColumn(
                name: "PlatoonPrimaryFrequency",
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
