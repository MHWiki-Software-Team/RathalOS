using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RathalOS.Migrations
{
    /// <inheritdoc />
    public partial class MHHCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MHHCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CardId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HunterWeapon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HunterArmor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Decoration = table.Column<int>(type: "int", nullable: false),
                    WikiUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MHHCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MHHCards_WikiUsers_WikiUserId",
                        column: x => x.WikiUserId,
                        principalTable: "WikiUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MHHCards_WikiUserId",
                table: "MHHCards",
                column: "WikiUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MHHCards");
        }
    }
}
