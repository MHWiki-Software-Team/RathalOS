using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RathalOS.Migrations
{
    /// <inheritdoc />
    public partial class Caching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Guid",
                table: "MHHCards",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "MHHCardStorage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Series = table.Column<int>(type: "int", nullable: false),
                    CardNumber = table.Column<int>(type: "int", nullable: false),
                    Decoration = table.Column<int>(type: "int", nullable: false),
                    StoredCard = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MHHCardStorage", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MHHCardStorage");

            migrationBuilder.DropColumn(
                name: "Guid",
                table: "MHHCards");
        }
    }
}
