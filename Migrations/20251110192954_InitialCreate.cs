using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraviOS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WikiUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserID = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WikiTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActive = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChannelID = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    CreatorId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TagsCSV = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    OnHold = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WikiTasks_WikiUsers_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "WikiUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssignedTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssigneeId = table.Column<int>(type: "int", nullable: true),
                    AssignmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignedTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignedTasks_WikiTasks_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "WikiTasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssignedTasks_WikiUsers_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "WikiUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WikiTaskUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<int>(type: "int", nullable: true),
                    Update = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiTaskUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WikiTaskUpdates_WikiTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "WikiTasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WikiTaskUpdates_WikiUsers_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "WikiUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTasks_AssigneeId",
                table: "AssignedTasks",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTasks_AssignmentId",
                table: "AssignedTasks",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiTasks_CreatorId",
                table: "WikiTasks",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiTaskUpdates_CreatorId",
                table: "WikiTaskUpdates",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiTaskUpdates_TaskId",
                table: "WikiTaskUpdates",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignedTasks");

            migrationBuilder.DropTable(
                name: "WikiTaskUpdates");

            migrationBuilder.DropTable(
                name: "WikiTasks");

            migrationBuilder.DropTable(
                name: "WikiUsers");
        }
    }
}
