using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostVision.Infrastructure.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RefreshRevision",
                table: "Receipts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Receipts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefreshRevision",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Receipts");
        }
    }
}
