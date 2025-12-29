using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyMonitoringSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterConsumptionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeterConsumptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeterId = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Consumption = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterConsumptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeterConsumptions_MeterId_PeriodType_Timestamp",
                table: "MeterConsumptions",
                columns: new[] { "MeterId", "PeriodType", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeterConsumptions");
        }
    }
}
