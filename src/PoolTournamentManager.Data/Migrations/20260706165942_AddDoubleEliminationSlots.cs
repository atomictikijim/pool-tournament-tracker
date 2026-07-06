using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDoubleEliminationSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeedsIntoLoserSlot",
                table: "BracketNodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeedsIntoWinnerSlot",
                table: "BracketNodes",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedsIntoLoserSlot",
                table: "BracketNodes");

            migrationBuilder.DropColumn(
                name: "FeedsIntoWinnerSlot",
                table: "BracketNodes");
        }
    }
}
