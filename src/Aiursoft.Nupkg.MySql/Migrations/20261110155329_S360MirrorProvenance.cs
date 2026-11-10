using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.Nupkg.MySql.Migrations
{
    /// <inheritdoc />
    public partial class S360MirrorProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocallyPublished",
                table: "Packages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MirrorSha256",
                table: "Packages",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MirrorSource",
                table: "Packages",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MirrorVersion",
                table: "Packages",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.Sql("UPDATE Packages SET IsLocallyPublished = 1 WHERE UploadedByUserId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocallyPublished",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "MirrorSha256",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "MirrorSource",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "MirrorVersion",
                table: "Packages");
        }
    }
}
