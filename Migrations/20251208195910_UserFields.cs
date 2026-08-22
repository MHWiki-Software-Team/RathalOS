using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RathalOS.Migrations
{
    /// <inheritdoc />
    public partial class UserFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Boosters",
                table: "WikiUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FavoriteCardJson",
                table: "WikiUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LastEditCount",
                table: "WikiUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LifetimeBoosters",
                table: "WikiUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LifetimePulls",
                table: "WikiUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Pulls",
                table: "WikiUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WikiUsername",
                table: "WikiUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Boosters",
                table: "WikiUsers");

            migrationBuilder.DropColumn(
                name: "FavoriteCardJson",
                table: "WikiUsers");

            migrationBuilder.DropColumn(
                name: "LastEditCount",
                table: "WikiUsers");

            migrationBuilder.DropColumn(
                name: "LifetimeBoosters",
                table: "WikiUsers");

            migrationBuilder.DropColumn(
                name: "LifetimePulls",
                table: "WikiUsers");

            migrationBuilder.DropColumn(
                name: "Pulls",
                table: "WikiUsers");

            migrationBuilder.DropColumn(
                name: "WikiUsername",
                table: "WikiUsers");
        }
    }
}
