using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BankAccountId",
                table: "Invoices",
                column: "BankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_BankAccounts_BankAccountId",
                table: "Invoices",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_BankAccounts_BankAccountId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_BankAccountId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Invoices");
        }
    }
}
