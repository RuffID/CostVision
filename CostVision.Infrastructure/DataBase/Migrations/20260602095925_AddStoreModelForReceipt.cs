using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostVision.Infrastructure.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreModelForReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "Receipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    NormalizedAddress = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AdaptiveName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                WITH SourceStores AS
                (
                    SELECT
                        LTRIM(RTRIM(ISNULL(RetailPlace, N''))) AS Name,
                        LTRIM(RTRIM(ISNULL(RetailPlaceAddress, N''))) AS Address,
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(LTRIM(RTRIM(ISNULL(RetailPlace, N'')))), N'Ё', N'Е'), N' ', N''), N'.', N''), N',', N''), N'-', N''), N'/', N'') AS NormalizedName,
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(LTRIM(RTRIM(ISNULL(RetailPlaceAddress, N'')))), N'Ё', N'Е'), N' ', N''), N'.', N''), N',', N''), N'-', N''), N'/', N'') AS NormalizedAddress
                    FROM Receipts
                    WHERE NULLIF(LTRIM(RTRIM(ISNULL(RetailPlace, N''))), N'') IS NOT NULL
                       OR NULLIF(LTRIM(RTRIM(ISNULL(RetailPlaceAddress, N''))), N'') IS NOT NULL
                )
                INSERT INTO Stores (Name, NormalizedName, Address, NormalizedAddress)
                SELECT
                    MIN(Name) AS Name,
                    NormalizedName,
                    MIN(Address) AS Address,
                    NormalizedAddress
                FROM SourceStores
                GROUP BY NormalizedName, NormalizedAddress;
                """);

            migrationBuilder.Sql(
                """
                UPDATE receipt
                SET StoreId = store.Id
                FROM Receipts AS receipt
                INNER JOIN Stores AS store
                    ON store.NormalizedName = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(LTRIM(RTRIM(ISNULL(receipt.RetailPlace, N'')))), N'Ё', N'Е'), N' ', N''), N'.', N''), N',', N''), N'-', N''), N'/', N'')
                   AND store.NormalizedAddress = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(LTRIM(RTRIM(ISNULL(receipt.RetailPlaceAddress, N'')))), N'Ё', N'Е'), N' ', N''), N'.', N''), N',', N''), N'-', N''), N'/', N'')
                WHERE NULLIF(LTRIM(RTRIM(ISNULL(receipt.RetailPlace, N''))), N'') IS NOT NULL
                   OR NULLIF(LTRIM(RTRIM(ISNULL(receipt.RetailPlaceAddress, N''))), N'') IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_StoreId",
                table: "Receipts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_NormalizedName_NormalizedAddress",
                table: "Stores",
                columns: new[] { "NormalizedName", "NormalizedAddress" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_Stores_StoreId",
                table: "Receipts",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "RetailPlace",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "RetailPlaceAddress",
                table: "Receipts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RetailPlace",
                table: "Receipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetailPlaceAddress",
                table: "Receipts",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE receipt
                SET
                    RetailPlace = NULLIF(store.Name, N''),
                    RetailPlaceAddress = NULLIF(store.Address, N'')
                FROM Receipts AS receipt
                INNER JOIN Stores AS store
                    ON store.Id = receipt.StoreId;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_Stores_StoreId",
                table: "Receipts");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_StoreId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Receipts");
        }
    }
}
