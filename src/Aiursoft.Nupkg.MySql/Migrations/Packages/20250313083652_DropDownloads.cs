using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.Nupkg.MySql.Migrations.Packages
{
    /// <inheritdoc />
    public partial class DropDownloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Downloads",
                table: "Packages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Downloads",
                table: "Packages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
