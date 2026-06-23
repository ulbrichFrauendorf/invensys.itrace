using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerformanceCounterDailySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    Minimum = table.Column<double>(type: "float", nullable: false),
                    Maximum = table.Column<double>(type: "float", nullable: false),
                    Average = table.Column<double>(type: "float", nullable: false),
                    AlertHighCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceCounterDailySummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceCounterSamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CpuUsagePercent = table.Column<double>(type: "float", nullable: true),
                    MemoryUsagePercent = table.Column<double>(type: "float", nullable: true),
                    MemoryUsedBytes = table.Column<long>(type: "bigint", nullable: true),
                    MemoryLimitBytes = table.Column<long>(type: "bigint", nullable: true),
                    DiskUsagePercent = table.Column<double>(type: "float", nullable: true),
                    DiskUsedBytes = table.Column<long>(type: "bigint", nullable: true),
                    DiskTotalBytes = table.Column<long>(type: "bigint", nullable: true),
                    NetworkReceiveBytesPerSecond = table.Column<double>(type: "float", nullable: true),
                    NetworkTransmitBytesPerSecond = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceCounterSamples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceCounterDailySummaries_Day_Scope_SourceId_Metric",
                table: "PerformanceCounterDailySummaries",
                columns: new[] { "Day", "Scope", "SourceId", "Metric" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceCounterSamples_Scope_SourceId_OccurredAtUtc",
                table: "PerformanceCounterSamples",
                columns: new[] { "Scope", "SourceId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformanceCounterDailySummaries");

            migrationBuilder.DropTable(
                name: "PerformanceCounterSamples");
        }
    }
}
