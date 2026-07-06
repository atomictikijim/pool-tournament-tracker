using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRingGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RingGameDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BuyInAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    FiveBallPayout = table.Column<decimal>(type: "TEXT", nullable: false),
                    NineBallPayout = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrentRackNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentShooterEntrantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingGameDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingGameDetails_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingGameDetailId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntrantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    RackNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLedgerEntries_RingGameDetails_RingGameDetailId",
                        column: x => x.RingGameDetailId,
                        principalTable: "RingGameDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RingLedgerEntries_TournamentEntrants_EntrantId",
                        column: x => x.EntrantId,
                        principalTable: "TournamentEntrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingGameDetails_TournamentId",
                table: "RingGameDetails",
                column: "TournamentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLedgerEntries_EntrantId",
                table: "RingLedgerEntries",
                column: "EntrantId");

            migrationBuilder.CreateIndex(
                name: "IX_RingLedgerEntries_RingGameDetailId",
                table: "RingLedgerEntries",
                column: "RingGameDetailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingLedgerEntries");

            migrationBuilder.DropTable(
                name: "RingGameDetails");
        }
    }
}
