using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProxyManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    operation = table.Column<int>(type: "integer", nullable: false),
                    proxy_host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_state = table.Column<string>(type: "text", nullable: true),
                    new_state = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "proxy_hosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain_names = table.Column<List<string>>(type: "text[]", nullable: false),
                    destination_scheme = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    destination_host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    destination_port = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    certificate_path = table.Column<string>(type: "text", nullable: true),
                    certificate_key_path = table.Column<string>(type: "text", nullable: true),
                    certificate_password = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proxy_hosts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred_at",
                table: "audit_log_entries",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropTable(
                name: "proxy_hosts");
        }
    }
}
