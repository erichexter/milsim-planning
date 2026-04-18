using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventOwnerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add CreatedById column to Events table
            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "Events",
                type: "text",
                nullable: true);  // Allow null initially, then populate

            // Populate CreatedById for existing events from the Faction.CommanderId
            // (Each event's creator is the faction commander in the current 1:1 Event-Faction model)
            migrationBuilder.Sql(@"
                UPDATE ""Events"" e
                SET ""CreatedById"" = f.""CommanderId""
                FROM ""Factions"" f
                WHERE e.""Id"" = f.""EventId"";
            ");

            // Now make CreatedById NOT NULL after population
            migrationBuilder.AlterColumn<string>(
                name: "CreatedById",
                table: "Events",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Create EventOwner role assignments for existing event creators
            migrationBuilder.Sql(@"
                INSERT INTO ""EventMemberships"" (""Id"", ""UserId"", ""EventId"", ""Role"", ""JoinedAt"")
                SELECT
                    gen_random_uuid(),
                    f.""CommanderId"",
                    e.""Id"",
                    'event_owner',
                    CURRENT_TIMESTAMP
                FROM ""Events"" e
                INNER JOIN ""Factions"" f ON e.""Id"" = f.""EventId""
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""EventMemberships"" m
                    WHERE m.""UserId"" = f.""CommanderId""
                    AND m.""EventId"" = e.""Id""
                    AND m.""Role"" = 'event_owner'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete EventOwner role records created in Up migration
            migrationBuilder.Sql(@"
                DELETE FROM ""EventMemberships""
                WHERE ""Role"" = 'event_owner'
                AND ""JoinedAt"" >= CURRENT_TIMESTAMP - interval '1 day';
            ");

            // Drop CreatedById column
            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Events");
        }
    }
}
