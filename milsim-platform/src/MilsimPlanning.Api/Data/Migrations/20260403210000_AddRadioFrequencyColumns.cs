using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRadioFrequencyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommandPrimaryFrequency",
                table: "Factions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommandBackupFrequency",
                table: "Factions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryFrequency",
                table: "Platoons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackupFrequency",
                table: "Platoons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryFrequency",
                table: "Squads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackupFrequency",
                table: "Squads",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CommandPrimaryFrequency", table: "Factions");
            migrationBuilder.DropColumn(name: "CommandBackupFrequency", table: "Factions");
            migrationBuilder.DropColumn(name: "PrimaryFrequency", table: "Platoons");
            migrationBuilder.DropColumn(name: "BackupFrequency", table: "Platoons");
            migrationBuilder.DropColumn(name: "PrimaryFrequency", table: "Squads");
            migrationBuilder.DropColumn(name: "BackupFrequency", table: "Squads");
        }
    }
}
