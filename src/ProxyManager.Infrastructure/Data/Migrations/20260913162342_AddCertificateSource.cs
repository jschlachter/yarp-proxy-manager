using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProxyManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "certificates",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source",
                table: "certificates");
        }
    }
}
