using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RathalOS.Migrations
{
    /// <inheritdoc />
    public partial class usertrim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastEditCount",
                table: "WikiUsers",
                newName: "GrantPulls");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActiveCheck",
                table: "WikiUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastActiveCheck",
                table: "WikiUsers");

            migrationBuilder.RenameColumn(
                name: "GrantPulls",
                table: "WikiUsers",
                newName: "LastEditCount");
        }
    }
}
