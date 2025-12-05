using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VatCode",
                table: "Companies",
                newName: "RegNumber");

            migrationBuilder.AddColumn<string>(
                name: "Bank",
                table: "Companies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CUI",
                table: "Companies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IBAN",
                table: "Companies",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bank",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CUI",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IBAN",
                table: "Companies");

            migrationBuilder.RenameColumn(
                name: "RegNumber",
                table: "Companies",
                newName: "VatCode");
        }
    }
}
