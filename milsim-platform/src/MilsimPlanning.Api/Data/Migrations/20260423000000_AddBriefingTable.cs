using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBriefingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Briefings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ChannelIdentifier = table.Column<string>(type: "text", nullable: false),
                    PublicationState = table.Column<string>(type: "text", nullable: false),
                    VersionETag = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Briefings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Briefings_ChannelIdentifier",
                table: "Briefings",
                column: "ChannelIdentifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Briefings");
        }
    }
}
