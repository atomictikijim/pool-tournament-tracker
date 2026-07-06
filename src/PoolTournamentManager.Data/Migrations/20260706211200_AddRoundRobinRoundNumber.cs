using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundRobinRoundNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoundNumber",
                table: "Matches",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoundNumber",
                table: "Matches");
        }
    }
}
