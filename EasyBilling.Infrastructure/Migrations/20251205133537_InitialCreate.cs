using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    RegNumber = table.Column<string>(type: "text", nullable: true),
                    CUI = table.Column<string>(type: "text", nullable: false),
                    IBAN = table.Column<string>(type: "text", nullable: true),
                    Bank = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    CUI = table.Column<string>(type: "text", nullable: false),
                    RegNumber = table.Column<string>(type: "text", nullable: true),
                    IBAN = table.Column<string>(type: "text", nullable: true),
                    Bank = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                columns: new[] { "Id", "Address", "Bank", "CUI", "IBAN", "Name", "RegNumber", "UserId" },
                values: new object[,]
                {
                    { new Guid("40019908-3df7-4764-bbb7-1776e8e23245"), "Str. Testului 2, Cluj-Napoca, Romania", "Banca Transilvania", "RO87654321", "RO49BBBB1B31007593840000", "Demo Client SRL", "J12/567/2020", new Guid("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303") },
                    { new Guid("e11e24c2-8c61-4adb-af89-9464ac44964a"), "Strada 14 Octombrie 115B, Targu Jiu, Gorj", "Revolut Bank UAD", "RO49311115", "RO49AAAA1B31007593840000", "SAMTECH LABS SRL", "J18/1171/2023", new Guid("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CompanyId",
                table: "Clients",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_UserId",
                table: "Companies",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
