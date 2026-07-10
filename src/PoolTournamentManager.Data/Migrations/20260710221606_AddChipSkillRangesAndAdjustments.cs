using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChipSkillRangesAndAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChipAdjustment",
                table: "TournamentEntrants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingChips",
                table: "TournamentEntrants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChipRatingSystem",
                table: "ChipGameDetails",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChipStartingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChipGameDetailId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MinRating = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxRating = table.Column<int>(type: "INTEGER", nullable: true),
                    Chips = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChipStartingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChipStartingRules_ChipGameDetails_ChipGameDetailId",
                        column: x => x.ChipGameDetailId,
                        principalTable: "ChipGameDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChipStartingRules_ChipGameDetailId",
                table: "ChipStartingRules",
                column: "ChipGameDetailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChipStartingRules");

            migrationBuilder.DropColumn(
                name: "ChipAdjustment",
                table: "TournamentEntrants");

            migrationBuilder.DropColumn(
                name: "StartingChips",
                table: "TournamentEntrants");

            migrationBuilder.DropColumn(
                name: "ChipRatingSystem",
                table: "ChipGameDetails");
        }
    }
}
