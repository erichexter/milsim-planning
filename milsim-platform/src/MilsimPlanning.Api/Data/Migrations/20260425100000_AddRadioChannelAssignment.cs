using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRadioChannelAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add CallSign to RadioChannels (Story 4 requirement)
            migrationBuilder.AddColumn<string>(
                name: "CallSign",
                table: "RadioChannels",
                type: "text",
                nullable: true);

            // RadioChannelAssignments table — polymorphic unit reference
            migrationBuilder.CreateTable(
                name: "RadioChannelAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    SquadId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlatoonId = table.Column<Guid>(type: "uuid", nullable: true),
                    FactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Primary = table.Column<decimal>(type: "decimal(7,3)", nullable: true),
                    Alternate = table.Column<decimal>(type: "decimal(7,3)", nullable: true),
                    HasConflict = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadioChannelAssignments", x => x.Id);

                    table.ForeignKey(
                        name: "FK_RadioChannelAssignments_RadioChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "RadioChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_RadioChannelAssignments_Squads_SquadId",
                        column: x => x.SquadId,
                        principalTable: "Squads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);

                    table.ForeignKey(
                        name: "FK_RadioChannelAssignments_Platoons_PlatoonId",
                        column: x => x.PlatoonId,
                        principalTable: "Platoons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);

                    table.ForeignKey(
                        name: "FK_RadioChannelAssignments_AspNetUsers_FactionId",
                        column: x => x.FactionId,
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Indexes for conflict detection lookups
            migrationBuilder.CreateIndex(
                name: "IX_RadioChannelAssignments_ChannelId_Primary",
                table: "RadioChannelAssignments",
                columns: new[] { "ChannelId", "Primary" });

            migrationBuilder.CreateIndex(
                name: "IX_RadioChannelAssignments_ChannelId_Alternate",
                table: "RadioChannelAssignments",
                columns: new[] { "ChannelId", "Alternate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RadioChannelAssignments");

            migrationBuilder.DropColumn(
                name: "CallSign",
                table: "RadioChannels");
        }
    }
}
