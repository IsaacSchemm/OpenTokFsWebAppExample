using Microsoft.EntityFrameworkCore.Migrations;

namespace WebApplication.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VonageVideoAPIProjectCredentials",
                columns: table => new
                {
                    ApiKey = table.Column<int>(type: "int", nullable: false),
                    ApiSecret = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VonageVideoAPIProjectCredentials", x => x.ApiKey);
                });

            migrationBuilder.CreateTable(
                name: "VonageVideoAPISessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProjectApiKey = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VonageVideoAPISessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_VonageVideoAPISessions_VonageVideoAPIProjectCredentials_ProjectApiKey",
                        column: x => x.ProjectApiKey,
                        principalTable: "VonageVideoAPIProjectCredentials",
                        principalColumn: "ApiKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VonageVideoAPISessions_ProjectApiKey",
                table: "VonageVideoAPISessions",
                column: "ProjectApiKey");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VonageVideoAPISessions");

            migrationBuilder.DropTable(
                name: "VonageVideoAPIProjectCredentials");
        }
    }
}
