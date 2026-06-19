using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SiteName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Dsn = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DsnHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Signal = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Route = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    DurationMs = table.Column<double>(type: "float", nullable: true),
                    Database = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DbSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DbStatement = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    ExceptionType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SpanId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AttributesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IngestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryRecords_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_DsnHash",
                table: "Applications",
                column: "DsnHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name_Environment_SiteName",
                table: "Applications",
                columns: new[] { "Name", "Environment", "SiteName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryRecords_ApplicationId_Signal_OccurredAtUtc",
                table: "TelemetryRecords",
                columns: new[] { "ApplicationId", "Signal", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryRecords_TraceId",
                table: "TelemetryRecords",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelemetryRecords");

            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
