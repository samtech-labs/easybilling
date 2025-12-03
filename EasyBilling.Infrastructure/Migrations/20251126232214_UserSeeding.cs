using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Client_Id", "Client_Secret", "Email", "Username" },
                values: new object[,]
                {
                    { new Guid("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303"), new Guid("c2a4f8b1-6e5d-4f17-91bb-0f92b74f4404"), "user123", "user@gmail.com", "user" },
                    { new Guid("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101"), new Guid("e8c9d14f-3df0-4ab5-9a72-6c1f4bb3a202"), "admin123", "admin@gmail.com", "admin" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101"));
        }
    }
}
