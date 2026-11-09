using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.Nupkg.MySql.Migrations
{
    /// <inheritdoc />
    public partial class PersonalPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PackageFamilyId",
                table: "Packages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UploadedByUserId",
                table: "Packages",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PackageFamilies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PackageId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwnerUserId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageFamilies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageFamilies_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KeyHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KeyPrefix = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserApiKeys_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PackageFamilyId",
                table: "Packages",
                column: "PackageFamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_UploadedByUserId",
                table: "Packages",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageFamilies_NormalizedId",
                table: "PackageFamilies",
                column: "NormalizedId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageFamilies_OwnerUserId",
                table: "PackageFamilies",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserApiKeys_KeyHash",
                table: "UserApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserApiKeys_UserId",
                table: "UserApiKeys",
                column: "UserId");

            // Preserve every existing version and reserve its name without inventing an owner.
            migrationBuilder.Sql("""
                INSERT INTO `PackageFamilies` (`PackageId`, `NormalizedId`, `CreatedAt`)
                SELECT MIN(`Id`), LOWER(`Id`), MIN(`Published`)
                FROM `Packages` GROUP BY LOWER(`Id`);
                """);
            migrationBuilder.Sql("""
                UPDATE `Packages` SET `PackageFamilyId` =
                (SELECT `Id` FROM `PackageFamilies`
                 WHERE `NormalizedId` = LOWER(`Packages`.`Id`));
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_AspNetUsers_UploadedByUserId",
                table: "Packages",
                column: "UploadedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_PackageFamilies_PackageFamilyId",
                table: "Packages",
                column: "PackageFamilyId",
                principalTable: "PackageFamilies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packages_AspNetUsers_UploadedByUserId",
                table: "Packages");

            migrationBuilder.DropForeignKey(
                name: "FK_Packages_PackageFamilies_PackageFamilyId",
                table: "Packages");

            migrationBuilder.DropTable(
                name: "PackageFamilies");

            migrationBuilder.DropTable(
                name: "UserApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_Packages_PackageFamilyId",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_UploadedByUserId",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "PackageFamilyId",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "Packages");
        }
    }
}
