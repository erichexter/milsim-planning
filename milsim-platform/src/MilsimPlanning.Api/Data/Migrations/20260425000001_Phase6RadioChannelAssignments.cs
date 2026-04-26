using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6RadioChannelAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RadioChannels table
            migrationBuilder.CreateTable(
                name: "RadioChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadioChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadioChannels_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RadioChannels_EventId",
                table: "RadioChannels",
                column: "EventId");

            // ChannelAssignments table
            migrationBuilder.CreateTable(
                name: "ChannelAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RadioChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    SquadId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryFrequency = table.Column<decimal>(type: "decimal(8,3)", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelAssignments_RadioChannels_RadioChannelId",
                        column: x => x.RadioChannelId,
                        principalTable: "RadioChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChannelAssignments_Squads_SquadId",
                        column: x => x.SquadId,
                        principalTable: "Squads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChannelAssignments_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelAssignments_EventId",
                table: "ChannelAssignments",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelAssignments_SquadId_RadioChannelId",
                table: "ChannelAssignments",
                columns: new[] { "SquadId", "RadioChannelId" });

            // FrequencyAuditLogs table
            migrationBuilder.CreateTable(
                name: "FrequencyAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitType = table.Column<string>(type: "text", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitName = table.Column<string>(type: "text", nullable: false),
                    PrimaryFrequency = table.Column<string>(type: "text", nullable: true),
                    AlternateFrequency = table.Column<string>(type: "text", nullable: true),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    ConflictingUnitName = table.Column<string>(type: "text", nullable: true),
                    PerformedByUserId = table.Column<string>(type: "text", nullable: false),
                    PerformedByDisplayName = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrequencyAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrequencyAuditLogs_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FrequencyAuditLogs_EventId_OccurredAt",
                table: "FrequencyAuditLogs",
                columns: new[] { "EventId", "OccurredAt" });

            // FrequencyConflicts table
            migrationBuilder.CreateTable(
                name: "FrequencyConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    FrequencyType = table.Column<string>(type: "text", nullable: false),
                    UnitAType = table.Column<string>(type: "text", nullable: false),
                    UnitAId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitAName = table.Column<string>(type: "text", nullable: false),
                    UnitBType = table.Column<string>(type: "text", nullable: false),
                    UnitBId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitBName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    ActionTaken = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrequencyConflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrequencyConflicts_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FrequencyConflicts_EventId_Status",
                table: "FrequencyConflicts",
                columns: new[] { "EventId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChannelAssignments");
            migrationBuilder.DropTable(name: "FrequencyAuditLogs");
            migrationBuilder.DropTable(name: "FrequencyConflicts");
            migrationBuilder.DropTable(name: "RadioChannels");
        }
    }
}
