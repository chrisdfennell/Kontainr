using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kontainr.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContainerMetrics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HostId = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerId = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CpuPercent = table.Column<double>(type: "REAL", nullable: false),
                    MemoryBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    NetworkRxBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    NetworkTxBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerMetrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMetrics_HostId_ContainerId_Timestamp",
                table: "ContainerMetrics",
                columns: new[] { "HostId", "ContainerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMetrics_Timestamp",
                table: "ContainerMetrics",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContainerMetrics");
        }
    }
}
