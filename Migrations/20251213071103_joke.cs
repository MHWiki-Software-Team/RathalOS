using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RathalOS.Migrations
{
    /// <inheritdoc />
    public partial class joke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Monkeys",
                table: "MHHEnvironmentVariables",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Monkeys",
                table: "MHHEnvironmentVariables");
        }
    }
}
