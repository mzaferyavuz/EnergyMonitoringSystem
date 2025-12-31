using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyMonitoringSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffTenantRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_TenantId",
                table: "Tariffs",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tariffs_Tenants_TenantId",
                table: "Tariffs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tariffs_Tenants_TenantId",
                table: "Tariffs");

            migrationBuilder.DropIndex(
                name: "IX_Tariffs_TenantId",
                table: "Tariffs");
        }
    }
}
