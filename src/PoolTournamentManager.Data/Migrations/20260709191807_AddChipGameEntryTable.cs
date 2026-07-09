using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChipGameEntryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TableId",
                table: "ChipGameEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChipGameEntries_TableId",
                table: "ChipGameEntries",
                column: "TableId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChipGameEntries_Tables_TableId",
                table: "ChipGameEntries",
                column: "TableId",
                principalTable: "Tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChipGameEntries_Tables_TableId",
                table: "ChipGameEntries");

            migrationBuilder.DropIndex(
                name: "IX_ChipGameEntries_TableId",
                table: "ChipGameEntries");

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "ChipGameEntries");
        }
    }
}
