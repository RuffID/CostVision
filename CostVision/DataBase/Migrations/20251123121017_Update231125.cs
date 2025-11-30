using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostVision.Database.migrations
{
    /// <inheritdoc />
    public partial class Update231125 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItems_Products_ProductId",
                table: "ReceiptItems");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItems_Products_ProductId",
                table: "ReceiptItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItems_Products_ProductId",
                table: "ReceiptItems");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItems_Products_ProductId",
                table: "ReceiptItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
