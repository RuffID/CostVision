using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostVision.Infrastructure.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptRefreshRetryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRefreshAttemptAtUtc",
                table: "Receipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastRefreshError",
                table: "Receipts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRefreshAttemptAtUtc",
                table: "Receipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefreshAttemptCount",
                table: "Receipts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RefreshStatus",
                table: "Receipts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_RefreshStatus_NextRefreshAttemptAtUtc",
                table: "Receipts",
                columns: new[] { "RefreshStatus", "NextRefreshAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_RefreshStatus_NextRefreshAttemptAtUtc",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "LastRefreshAttemptAtUtc",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "LastRefreshError",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "NextRefreshAttemptAtUtc",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "RefreshAttemptCount",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "RefreshStatus",
                table: "Receipts");
        }
    }
}
