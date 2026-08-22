using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RathalOS.Migrations
{
    /// <inheritdoc />
    public partial class Trades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TradeInventoryJson",
                table: "WikiUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MHHOpenTradeId",
                table: "MHHCards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MHHOpenTradeId1",
                table: "MHHCards",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MHHOpenTrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExecutorId = table.Column<int>(type: "int", nullable: false),
                    IsBuildingRecipientRequest = table.Column<bool>(type: "bit", nullable: false),
                    RecipientId = table.Column<int>(type: "int", nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MHHOpenTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MHHOpenTrades_WikiUsers_ExecutorId",
                        column: x => x.ExecutorId,
                        principalTable: "WikiUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_MHHOpenTrades_WikiUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "WikiUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MHHCards_MHHOpenTradeId",
                table: "MHHCards",
                column: "MHHOpenTradeId");

            migrationBuilder.CreateIndex(
                name: "IX_MHHCards_MHHOpenTradeId1",
                table: "MHHCards",
                column: "MHHOpenTradeId1");

            migrationBuilder.CreateIndex(
                name: "IX_MHHOpenTrades_ExecutorId",
                table: "MHHOpenTrades",
                column: "ExecutorId");

            migrationBuilder.CreateIndex(
                name: "IX_MHHOpenTrades_RecipientId",
                table: "MHHOpenTrades",
                column: "RecipientId");

            migrationBuilder.AddForeignKey(
                name: "FK_MHHCards_MHHOpenTrades_MHHOpenTradeId",
                table: "MHHCards",
                column: "MHHOpenTradeId",
                principalTable: "MHHOpenTrades",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MHHCards_MHHOpenTrades_MHHOpenTradeId1",
                table: "MHHCards",
                column: "MHHOpenTradeId1",
                principalTable: "MHHOpenTrades",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MHHCards_MHHOpenTrades_MHHOpenTradeId",
                table: "MHHCards");

            migrationBuilder.DropForeignKey(
                name: "FK_MHHCards_MHHOpenTrades_MHHOpenTradeId1",
                table: "MHHCards");

            migrationBuilder.DropTable(
                name: "MHHOpenTrades");

            migrationBuilder.DropIndex(
                name: "IX_MHHCards_MHHOpenTradeId",
                table: "MHHCards");

            migrationBuilder.DropIndex(
                name: "IX_MHHCards_MHHOpenTradeId1",
                table: "MHHCards");

            migrationBuilder.DropColumn(
                name: "TradeInventoryJson",
                table: "WikiUsers");

            migrationBuilder.DropColumn(
                name: "MHHOpenTradeId",
                table: "MHHCards");

            migrationBuilder.DropColumn(
                name: "MHHOpenTradeId1",
                table: "MHHCards");
        }
    }
}
