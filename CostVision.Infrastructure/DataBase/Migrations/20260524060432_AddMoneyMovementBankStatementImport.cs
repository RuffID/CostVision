using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostVision.Infrastructure.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyMovementBankStatementImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportComment",
                table: "MoneyMovements",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoneyMovements_AccountId_OccurredAt_Amount_Type",
                table: "MoneyMovements",
                columns: new[] { "AccountId", "OccurredAt", "Amount", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MoneyMovements_AccountId_OccurredAt_Amount_Type",
                table: "MoneyMovements");

            migrationBuilder.DropColumn(
                name: "ImportComment",
                table: "MoneyMovements");
        }
    }
}
