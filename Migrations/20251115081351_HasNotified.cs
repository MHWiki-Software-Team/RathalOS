using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RathalOS.Migrations
{
    /// <inheritdoc />
    public partial class HasNotified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasNotified",
                table: "ReleaseDates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasNotified",
                table: "ReleaseDates");
        }
    }
}
