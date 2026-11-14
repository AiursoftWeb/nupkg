using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.Nupkg.MySql.Migrations
{
    /// <inheritdoc />
    public partial class LocalPackageAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocallyPublished",
                table: "PackageFamilies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE PackageFamilies
                SET IsLocallyPublished = 1
                WHERE OwnerUserId IS NOT NULL OR EXISTS (
                    SELECT 1 FROM Packages
                    WHERE Packages.PackageFamilyId = PackageFamilies.Id
                      AND (Packages.IsLocallyPublished = 1 OR Packages.UploadedByUserId IS NOT NULL))
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocallyPublished",
                table: "PackageFamilies");
        }
    }
}
