using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentBracket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    GameType = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SeedingRatingSystem = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BracketDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsDoubleElimination = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BracketDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BracketDetails_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tables_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentEntrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeedNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    IsEliminated = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentEntrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentEntrants_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentEntrants_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BracketNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TableId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Player1EntrantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Player2EntrantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Player1Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Player2Score = table.Column<int>(type: "INTEGER", nullable: true),
                    WinnerEntrantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_TournamentEntrants_Player1EntrantId",
                        column: x => x.Player1EntrantId,
                        principalTable: "TournamentEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_TournamentEntrants_Player2EntrantId",
                        column: x => x.Player2EntrantId,
                        principalTable: "TournamentEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BracketNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BracketDetailId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionInRound = table.Column<int>(type: "INTEGER", nullable: false),
                    Slot1EntrantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Slot2EntrantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FeedsIntoWinnerNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FeedsIntoLoserNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsGrandFinalReset = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BracketNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BracketNodes_BracketDetails_BracketDetailId",
                        column: x => x.BracketDetailId,
                        principalTable: "BracketDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BracketNodes_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BracketDetails_TournamentId",
                table: "BracketDetails",
                column: "TournamentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BracketNodes_BracketDetailId",
                table: "BracketNodes",
                column: "BracketDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_BracketNodes_MatchId",
                table: "BracketNodes",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Player1EntrantId",
                table: "Matches",
                column: "Player1EntrantId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Player2EntrantId",
                table: "Matches",
                column: "Player2EntrantId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TableId",
                table: "Matches",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TournamentId",
                table: "Matches",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_TournamentId",
                table: "Tables",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEntrants_PlayerId",
                table: "TournamentEntrants",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEntrants_TournamentId",
                table: "TournamentEntrants",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BracketNodes");

            migrationBuilder.DropTable(
                name: "BracketDetails");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropTable(
                name: "TournamentEntrants");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
