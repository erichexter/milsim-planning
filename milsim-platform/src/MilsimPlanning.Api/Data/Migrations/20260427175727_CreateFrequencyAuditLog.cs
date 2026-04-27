using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateFrequencyAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FrequencyAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnitName = table.Column<string>(type: "text", nullable: false),
                    UnitType = table.Column<string>(type: "text", nullable: false),
                    PrimaryFrequency = table.Column<string>(type: "text", nullable: true),
                    AlternateFrequency = table.Column<string>(type: "text", nullable: true),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ConflictingUnitName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrequencyAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrequencyAuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrequencyAuditLogs_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FrequencyAuditLogs_EventId_Timestamp",
                table: "FrequencyAuditLogs",
                columns: new[] { "EventId", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FrequencyAuditLogs_EventId_UnitName",
                table: "FrequencyAuditLogs",
                columns: new[] { "EventId", "UnitName" });

            migrationBuilder.CreateIndex(
                name: "IX_FrequencyAuditLogs_UserId",
                table: "FrequencyAuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FrequencyAuditLogs");
        }
    }
}
