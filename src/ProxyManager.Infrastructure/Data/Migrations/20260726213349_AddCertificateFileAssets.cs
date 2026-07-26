using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProxyManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateFileAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "key_file_path",
                table: "certificates");

            // Clean break, not a rename: certificate_path held a filesystem path that nothing ever
            // wrote (see docs/files-service-plan.md); subject is unrelated X509 metadata. Assumes
            // the target DB has no live certificate rows, per that plan.
            migrationBuilder.DropColumn(
                name: "certificate_path",
                table: "certificates");

            migrationBuilder.AddColumn<string>(
                name: "subject",
                table: "certificates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "certificate_asset_id",
                table: "certificates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "certificate_file_name",
                table: "certificates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "key_asset_id",
                table: "certificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "key_file_name",
                table: "certificates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "not_after",
                table: "certificates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "not_before",
                table: "certificates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "subject_alternative_names",
                table: "certificates",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "thumbprint",
                table: "certificates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "certificate_asset_id",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "certificate_file_name",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "key_asset_id",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "key_file_name",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "not_after",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "not_before",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "subject_alternative_names",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "thumbprint",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "subject",
                table: "certificates");

            migrationBuilder.AddColumn<string>(
                name: "certificate_path",
                table: "certificates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "key_file_path",
                table: "certificates",
                type: "text",
                nullable: true);
        }
    }
}
