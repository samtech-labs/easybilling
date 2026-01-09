using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("40019908-3df7-4764-bbb7-1776e8e23245"));

            migrationBuilder.DeleteData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("e11e24c2-8c61-4adb-af89-9464ac44964a"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101"));

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "Password", "Username" },
                values: new object[,]
                {
                    { new Guid("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303"), "user@gmail.com", "user123", "user" },
                    { new Guid("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101"), "admin@gmail.com", "admin123", "admin" }
                });

            migrationBuilder.InsertData(
                table: "Companies",
                columns: new[] { "Id", "Address", "Bank", "CUI", "City", "Country", "County", "IBAN", "IsEFacturaActive", "IsVatPayer", "Name", "RegNumber", "UserId" },
                values: new object[,]
                {
                    { new Guid("40019908-3df7-4764-bbb7-1776e8e23245"), "Str. Testului 2, Cluj-Napoca, Romania", "Banca Transilvania", "RO87654321", null, null, null, "RO49BBBB1B31007593840000", false, false, "Demo Client SRL", "J12/567/2020", new Guid("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303") },
                    { new Guid("e11e24c2-8c61-4adb-af89-9464ac44964a"), "Strada 14 Octombrie 115B, Targu Jiu, Gorj", "Revolut Bank UAD", "RO49311115", null, null, null, "RO49AAAA1B31007593840000", false, false, "SAMTECH LABS SRL", "J18/1171/2023", new Guid("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101") }
                });
        }
    }
}
