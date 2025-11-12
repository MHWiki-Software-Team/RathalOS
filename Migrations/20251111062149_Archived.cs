using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraviOS.Migrations
{
    /// <inheritdoc />
    public partial class Archived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Archived",
                table: "WikiTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Archived",
                table: "WikiTasks");
        }
    }
}
