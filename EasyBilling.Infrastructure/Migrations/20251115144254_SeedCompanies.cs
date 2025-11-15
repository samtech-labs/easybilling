using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Companies",
                columns: new[] { "Id", "Address", "Bank", "CUI", "IBAN", "Name", "RegNumber" },
                values: new object[,]
                {
                    { new Guid("40019908-3df7-4764-bbb7-1776e8e23245"), "Str. Testului 2, Cluj-Napoca, Romania", "Banca Transilvania", "RO87654321", "RO49BBBB1B31007593840000", "Demo Client SRL", "J12/567/2020" },
                    { new Guid("e11e24c2-8c61-4adb-af89-9464ac44964a"), "Strada 14 Octombrie 115B, Targu Jiu, Gorj", "Revolut Bank UAD", "RO49311115", "RO49AAAA1B31007593840000", "SAMTECH LABS SRL", "J18/1171/2023" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("40019908-3df7-4764-bbb7-1776e8e23245"));

            migrationBuilder.DeleteData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("e11e24c2-8c61-4adb-af89-9464ac44964a"));
        }
    }
}
