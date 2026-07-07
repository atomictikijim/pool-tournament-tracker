using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChipTournament : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChipGameDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartingChips = table.Column<int>(type: "INTEGER", nullable: false),
                    BuyInAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    FirstPlacePayout = table.Column<decimal>(type: "TEXT", nullable: false),
                    SecondPlacePayout = table.Column<decimal>(type: "TEXT", nullable: false),
                    ThirdPlacePayout = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChipGameDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChipGameDetails_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChipGameEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChipGameDetailId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WinnerEntrantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LoserEntrantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChipGameEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChipGameEntries_ChipGameDetails_ChipGameDetailId",
                        column: x => x.ChipGameDetailId,
                        principalTable: "ChipGameDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChipGameEntries_TournamentEntrants_LoserEntrantId",
                        column: x => x.LoserEntrantId,
                        principalTable: "TournamentEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChipGameEntries_TournamentEntrants_WinnerEntrantId",
                        column: x => x.WinnerEntrantId,
                        principalTable: "TournamentEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChipGameDetails_TournamentId",
                table: "ChipGameDetails",
                column: "TournamentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChipGameEntries_ChipGameDetailId",
                table: "ChipGameEntries",
                column: "ChipGameDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ChipGameEntries_LoserEntrantId",
                table: "ChipGameEntries",
                column: "LoserEntrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChipGameEntries_WinnerEntrantId",
                table: "ChipGameEntries",
                column: "WinnerEntrantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChipGameEntries");

            migrationBuilder.DropTable(
                name: "ChipGameDetails");
        }
    }
}
