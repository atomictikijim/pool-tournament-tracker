using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsesTeams",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerId",
                table: "TournamentEntrants",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "TournamentEntrants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEntrants_TeamId",
                table: "TournamentEntrants",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentEntrants_Teams_TeamId",
                table: "TournamentEntrants",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentEntrants_Teams_TeamId",
                table: "TournamentEntrants");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_TournamentEntrants_TeamId",
                table: "TournamentEntrants");

            migrationBuilder.DropColumn(
                name: "UsesTeams",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "TournamentEntrants");

            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerId",
                table: "TournamentEntrants",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
