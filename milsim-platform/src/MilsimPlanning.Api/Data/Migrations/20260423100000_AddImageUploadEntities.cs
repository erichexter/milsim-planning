using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilsimPlanning.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUploadEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BriefingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedById = table.Column<string>(type: "text", nullable: false),
                    UploadStatus = table.Column<int>(type: "integer", nullable: false),
                    R2OriginalKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageUploads_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImageUploads_Briefings_BriefingId",
                        column: x => x.BriefingId,
                        principalTable: "Briefings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImageResizeJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JobCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TargetDimensions = table.Column<string>(type: "text", nullable: false),
                    ResizeStatus = table.Column<int>(type: "integer", nullable: false),
                    OutputR2Key = table.Column<string>(type: "text", nullable: true),
                    ErrorLog = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageResizeJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageResizeJobs_ImageUploads_ImageUploadId",
                        column: x => x.ImageUploadId,
                        principalTable: "ImageUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploads_BriefingId",
                table: "ImageUploads",
                column: "BriefingId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploads_UploadedById",
                table: "ImageUploads",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ImageResizeJobs_ImageUploadId",
                table: "ImageResizeJobs",
                column: "ImageUploadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageResizeJobs");

            migrationBuilder.DropTable(
                name: "ImageUploads");
        }
    }
}
