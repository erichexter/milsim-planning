using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlternateFrequencyToChannelAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Story 3 — add optional alternate frequency to squad channel assignments
            migrationBuilder.AddColumn<decimal>(
                name: "AlternateFrequency",
                table: "ChannelAssignments",
                type: "numeric(8,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlternateFrequency",
                table: "ChannelAssignments");
        }
    }
}
