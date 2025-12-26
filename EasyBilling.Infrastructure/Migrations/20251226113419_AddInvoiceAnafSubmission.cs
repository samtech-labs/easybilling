using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceAnafSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceAnafSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DownloadId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SentXml = table.Column<byte[]>(type: "bytea", nullable: true),
                    SignedXml = table.Column<byte[]>(type: "bytea", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceAnafSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceAnafSubmissions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAnafSubmissions_InvoiceId",
                table: "InvoiceAnafSubmissions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAnafSubmissions_Status",
                table: "InvoiceAnafSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAnafSubmissions_Status_LastCheckedAt",
                table: "InvoiceAnafSubmissions",
                columns: new[] { "Status", "LastCheckedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceAnafSubmissions");
        }
    }
}
