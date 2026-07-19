using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostVision.Infrastructure.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueReceiptIdentityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_CreatedByUserId",
                table: "Receipts");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_CreatedByUserId_FiscalDriveNumber_FiscalDocumentNumber_FiscalSign_DateTime_TotalSum_OperationType",
                table: "Receipts",
                columns: new[] { "CreatedByUserId", "FiscalDriveNumber", "FiscalDocumentNumber", "FiscalSign", "DateTime", "TotalSum", "OperationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_AdaptiveName",
                table: "Products",
                column: "AdaptiveName",
                unique: true,
                filter: "[AdaptiveName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_CreatedByUserId_FiscalDriveNumber_FiscalDocumentNumber_FiscalSign_DateTime_TotalSum_OperationType",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_Products_AdaptiveName",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_CreatedByUserId",
                table: "Receipts",
                column: "CreatedByUserId");
        }
    }
}
