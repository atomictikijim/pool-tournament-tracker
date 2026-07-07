using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryFeeAndPrizePayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EntryFee",
                table: "Tournaments",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HostFeePercentage",
                table: "Tournaments",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            // Carry over each existing chip tournament's fixed dollar buy-in into the new generic
            // EntryFee field before the old column is dropped below - a clean 1:1 value copy.
            // The old fixed-dollar 1st/2nd/3rd payouts have no safe equivalent conversion into the
            // new percentage-of-pool model (we can't know whether they summed to the full pot), so
            // no TournamentPrizePlaces rows are synthesized here - existing chip tournaments simply
            // have no configured payout places until reconfigured. TournamentFormat.ChipTournament = 4.
            migrationBuilder.Sql(@"
                UPDATE Tournaments
                SET EntryFee = COALESCE(
                    (SELECT BuyInAmount FROM ChipGameDetails WHERE ChipGameDetails.TournamentId = Tournaments.Id),
                    0)
                WHERE Format = 4;
            ");

            migrationBuilder.DropColumn(
                name: "BuyInAmount",
                table: "ChipGameDetails");

            migrationBuilder.DropColumn(
                name: "FirstPlacePayout",
                table: "ChipGameDetails");

            migrationBuilder.DropColumn(
                name: "SecondPlacePayout",
                table: "ChipGameDetails");

            migrationBuilder.DropColumn(
                name: "ThirdPlacePayout",
                table: "ChipGameDetails");

            migrationBuilder.CreateTable(
                name: "TournamentPrizePlaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Place = table.Column<int>(type: "INTEGER", nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentPrizePlaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentPrizePlaces_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPrizePlaces_TournamentId",
                table: "TournamentPrizePlaces",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentPrizePlaces");

            migrationBuilder.DropColumn(
                name: "EntryFee",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "HostFeePercentage",
                table: "Tournaments");

            migrationBuilder.AddColumn<decimal>(
                name: "BuyInAmount",
                table: "ChipGameDetails",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstPlacePayout",
                table: "ChipGameDetails",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SecondPlacePayout",
                table: "ChipGameDetails",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ThirdPlacePayout",
                table: "ChipGameDetails",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
