using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTournamentManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Tournaments",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Tournaments created before this column existed have no recorded creation time. Without
            // a backfill they would all stay at year 1 and sort arbitrarily among themselves, and
            // every new tournament would jump ahead of the user's entire existing history. Approximate
            // each old row's creation time from SQLite's rowid, which follows insertion order for
            // this ordinary (non-WITHOUT-ROWID) table: one second apart from a fixed epoch, so the
            // backfilled values are distinct, chronological, and still sort below anything created
            // after the upgrade.
            migrationBuilder.Sql(
                "UPDATE \"Tournaments\" SET \"CreatedAtUtc\" = " +
                "strftime('%Y-%m-%d %H:%M:%f', '2000-01-01 00:00:00', '+' || rowid || ' seconds')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Tournaments");
        }
    }
}
