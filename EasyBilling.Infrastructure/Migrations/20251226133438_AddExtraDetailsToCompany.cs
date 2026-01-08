using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraDetailsToCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEFacturaActive",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVatPayer",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("40019908-3df7-4764-bbb7-1776e8e23245"),
                columns: new[] { "City", "Country", "IsEFacturaActive", "IsVatPayer" },
                values: new object[] { null, null, false, false });

            migrationBuilder.UpdateData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("e11e24c2-8c61-4adb-af89-9464ac44964a"),
                columns: new[] { "City", "Country", "IsEFacturaActive", "IsVatPayer" },
                values: new object[] { null, null, false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsEFacturaActive",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsVatPayer",
                table: "Companies");
        }
    }
}
