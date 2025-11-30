using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostVision.Database.migrations
{
    /// <inheritdoc />
    public partial class AddProductModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItems_ExpenseCategories_CategoryId",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "ReceiptItems");

            migrationBuilder.AlterColumn<string>(
                name: "FiscalSign",
                table: "Receipts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "FiscalDriveNumber",
                table: "Receipts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "FiscalDocumentNumber",
                table: "Receipts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ProductId",
                table: "ReceiptItems",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItems_ExpenseCategories_CategoryId",
                table: "ReceiptItems",
                column: "CategoryId",
                principalTable: "ExpenseCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItems_Products_ProductId",
                table: "ReceiptItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItems_ExpenseCategories_CategoryId",
                table: "ReceiptItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItems_Products_ProductId",
                table: "ReceiptItems");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptItems_ProductId",
                table: "ReceiptItems");

            migrationBuilder.AlterColumn<string>(
                name: "FiscalSign",
                table: "Receipts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FiscalDriveNumber",
                table: "Receipts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FiscalDocumentNumber",
                table: "Receipts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ReceiptItems",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "ReceiptItems",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItems_ExpenseCategories_CategoryId",
                table: "ReceiptItems",
                column: "CategoryId",
                principalTable: "ExpenseCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
