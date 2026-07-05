using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProxyManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "certificate_key_path",
                table: "proxy_hosts");

            migrationBuilder.DropColumn(
                name: "certificate_password",
                table: "proxy_hosts");

            migrationBuilder.DropColumn(
                name: "certificate_path",
                table: "proxy_hosts");

            migrationBuilder.AddColumn<Guid>(
                name: "certificate_id",
                table: "proxy_hosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    certificate_path = table.Column<string>(type: "text", nullable: false),
                    key_file_path = table.Column<string>(type: "text", nullable: true),
                    pass_phrase = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certificates");

            migrationBuilder.DropColumn(
                name: "certificate_id",
                table: "proxy_hosts");

            migrationBuilder.AddColumn<string>(
                name: "certificate_key_path",
                table: "proxy_hosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_password",
                table: "proxy_hosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_path",
                table: "proxy_hosts",
                type: "text",
                nullable: true);
        }
    }
}
