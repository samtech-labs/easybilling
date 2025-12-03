using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Clients",
                columns: new[] { "Id", "Address", "Bank", "CUI", "Company_Id", "IBAN", "Name", "RegNumber" },
                values: new object[,]
                {
                    { new Guid("a3f5d9b2-1e34-4d5c-92a4-1d9c4c7b0151"), "Targu-Jiu, str. Spectaculosilor 14", "BCR", "RO12345678", new Guid("c13dbb54-9fc5-4c72-92df-c47e6dfcce21"), "RO49BCRL00001012345678", "SC Spectacol SRL", "J40/1234/2010" },
                    { new Guid("b7c89fa1-6bd2-4c26-a7ea-3b2cdb0f9e62"), "Tismana", "BT", "RO27833491", new Guid("de45bb29-fb3f-4c53-b9a0-87d13a6cc920"), "RO27BTRL0000123456789012", "Pandurii Tismana", "J12/567/2015" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: new Guid("a3f5d9b2-1e34-4d5c-92a4-1d9c4c7b0151"));

            migrationBuilder.DeleteData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: new Guid("b7c89fa1-6bd2-4c26-a7ea-3b2cdb0f9e62"));
        }
    }
}
