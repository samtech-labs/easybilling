using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtColumnToMembershipType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MembershipTypes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTypes_CreatedAt",
                table: "MembershipTypes",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MembershipTypes_CreatedAt",
                table: "MembershipTypes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MembershipTypes");
        }
    }
}
