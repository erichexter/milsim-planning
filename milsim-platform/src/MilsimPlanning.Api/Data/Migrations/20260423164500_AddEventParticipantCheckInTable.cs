using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventParticipantCheckInTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventParticipantCheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    QrCodeValue = table.Column<string>(type: "text", nullable: true),
                    ScannedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KioskId = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventParticipantCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventParticipantCheckIns_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventParticipantCheckIns_EventPlayers_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "EventPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipantCheckIns_EventId_ParticipantId",
                table: "EventParticipantCheckIns",
                columns: new[] { "EventId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipantCheckIns_ParticipantId",
                table: "EventParticipantCheckIns",
                column: "ParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventParticipantCheckIns");
        }
    }
}
