using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBracketKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDoubleElimination",
                table: "BracketDetails",
                newName: "Kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Kind",
                table: "BracketDetails",
                newName: "IsDoubleElimination");
        }
    }
}
