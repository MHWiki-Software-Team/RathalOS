using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RathalOS.Migrations
{
    /// <inheritdoc />
    public partial class EnvironmentVariables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MHHEnvironmentVariables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TotalPulls = table.Column<int>(type: "int", nullable: false),
                    LastHolo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSpecial = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRare = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentSpecialEdition = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MHHEnvironmentVariables", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MHHEnvironmentVariables");
        }
    }
}
