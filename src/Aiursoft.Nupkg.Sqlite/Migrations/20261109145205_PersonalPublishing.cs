using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.Nupkg.Sqlite.Migrations
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UploadedByUserId",
                table: "Packages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackageFamilies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NormalizedId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "UserApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                });

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
                INSERT INTO "PackageFamilies" ("PackageId", "NormalizedId", "CreatedAt")
                SELECT MIN("Id"), LOWER("Id"), MIN("Published")
                FROM "Packages" GROUP BY LOWER("Id");
                """);
            migrationBuilder.Sql("""
                UPDATE "Packages" SET "PackageFamilyId" =
                (SELECT "Id" FROM "PackageFamilies"
                 WHERE "NormalizedId" = LOWER("Packages"."Id"));
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
            // SQLite rebuilds Packages after the other schema operations. Keep the
            // parent table removable until that rebuild removes its foreign key.
            // The provider's rebuild restores foreign-key enforcement afterwards.
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

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
