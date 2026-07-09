using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class ChipGameServiceTests
{
    private static Tournament MakeChip(params string[] names)
    {
        var tournament = new Tournament { Name = "Chips", Format = TournamentFormat.ChipTournament };
        foreach (var name in names)
        {
            tournament.Entrants.Add(new TournamentEntrant
            {
                TournamentId = tournament.Id,
                PlayerId = Guid.NewGuid(),
                Player = new Player { FirstName = name, LastName = name }
            });
        }
        return tournament;
    }

    private static void AddTables(Tournament tournament, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            tournament.Tables.Add(new Table { TournamentId = tournament.Id, Label = $"Table {i}" });
        }
    }

    private static ChipGameService Service() => new();

    private static ChipStandingRow Row(Tournament t, TournamentEntrant e) =>
        ChipGameService.ComputeStandings(t).First(r => r.Entrant.Id == e.Id);

    private static Guid TableFor(Tournament t, Guid player1, Guid player2)
    {
        var board = ChipGameService.ComputeTableBoard(t);
        var seat = board.Tables.First(s =>
            (s.Player1?.Id == player1 && s.Player2?.Id == player2) ||
            (s.Player1?.Id == player2 && s.Player2?.Id == player1));
        return seat.Table.Id;
    }

    [Fact]
    public void Start_SetsInProgress_GivesEveryoneChips()
    {
        var t = MakeChip("A", "B", "C");
        var detail = Service().StartChipTournament(t, startingChips: 3);

        Assert.Equal(TournamentStatus.InProgress, t.Status);
        Assert.Equal(3, detail.StartingChips);
        Assert.All(t.Entrants, e => Assert.False(e.IsEliminated));
        Assert.All(t.Entrants, e => Assert.Equal(3, Row(t, e).ChipsRemaining));
    }

    [Fact]
    public void Start_RejectsFewerThanTwoPlayers_AndZeroChips()
    {
        Assert.Throws<InvalidOperationException>(() => Service().StartChipTournament(MakeChip("Solo"), 3));
        Assert.Throws<InvalidOperationException>(() => Service().StartChipTournament(MakeChip("A", "B"), 0));
    }

    [Fact]
    public void RecordGame_LoserDropsAChip_WinnerUnchanged()
    {
        var t = MakeChip("A", "B");
        var svc = Service();
        svc.StartChipTournament(t, 3);
        AddTables(t, 1);

        svc.RecordGame(t, t.Tables[0].Id, winnerId: t.Entrants[0].Id, loserId: t.Entrants[1].Id);

        Assert.Equal(3, Row(t, t.Entrants[0]).ChipsRemaining); // winner unchanged (lives rule)
        Assert.Equal(2, Row(t, t.Entrants[1]).ChipsRemaining); // loser down one
    }

    [Fact]
    public void RecordGame_RejectsSamePlayer_AndEliminatedPlayer()
    {
        var t = MakeChip("A", "B");
        var svc = Service();
        svc.StartChipTournament(t, 1);
        AddTables(t, 1);
        var tableId = t.Tables[0].Id;
        var a = t.Entrants[0].Id;
        var b = t.Entrants[1].Id;

        Assert.Throws<InvalidOperationException>(() => svc.RecordGame(t, tableId, a, a));

        svc.RecordGame(t, tableId, a, b); // B loses its only chip -> eliminated, tournament completes
        Assert.Throws<InvalidOperationException>(() => svc.RecordGame(t, tableId, a, b)); // B is out
    }

    [Fact]
    public void RecordGame_RejectsOutcomeThatDoesNotMatchCurrentSeating()
    {
        var t = MakeChip("A", "B", "C");
        var svc = Service();
        svc.StartChipTournament(t, 3);
        AddTables(t, 1);
        var tableId = t.Tables[0].Id;
        var a = t.Entrants[0].Id;
        var c = t.Entrants[2].Id;

        // Only one table exists, so the initial two entrants (A, B, in list order) are seated
        // there; C is waiting in NextUp. Submitting A vs C at that table doesn't match reality.
        var ex = Assert.Throws<InvalidOperationException>(() => svc.RecordGame(t, tableId, a, c));
        Assert.Contains("seating just changed", ex.Message);
    }

    [Fact]
    public void PlayerIsEliminatedAtZeroChips_AndTournamentCompletesWithChampionFirst()
    {
        var t = MakeChip("A", "B");
        var svc = Service();
        svc.StartChipTournament(t, 2);
        AddTables(t, 1);
        var tableId = t.Tables[0].Id;
        var a = t.Entrants[0];
        var b = t.Entrants[1];

        svc.RecordGame(t, tableId, a.Id, b.Id); // B: 2 -> 1
        Assert.False(b.IsEliminated);
        Assert.Equal(TournamentStatus.InProgress, t.Status);

        svc.RecordGame(t, tableId, a.Id, b.Id); // B: 1 -> 0, eliminated
        Assert.True(b.IsEliminated);
        Assert.Equal(TournamentStatus.Completed, t.Status);

        var standings = ChipGameService.ComputeStandings(t);
        Assert.Equal(a.Id, standings[0].Entrant.Id); // champion on top
        Assert.Equal(1, standings[0].Place);
        Assert.Equal(2, standings[1].Place); // B finishes 2nd
    }

    [Fact]
    public void FinishingOrder_FirstOut_FinishesLast()
    {
        var t = MakeChip("A", "B", "C");
        var svc = Service();
        svc.StartChipTournament(t, startingChips: 1);
        AddTables(t, 1); // A, B seed together (list order); C waits in NextUp
        var tableId = t.Tables[0].Id;
        var a = t.Entrants[0];
        var b = t.Entrants[1];
        var c = t.Entrants[2];

        svc.RecordGame(t, tableId, a.Id, b.Id); // B out first -> place 3 (last); C fills the vacancy
        svc.RecordGame(t, tableId, a.Id, c.Id); // C out second -> place 2; A champion -> place 1
        Assert.Equal(TournamentStatus.Completed, t.Status);

        Assert.Equal(1, Row(t, a).Place);
        Assert.Equal(2, Row(t, c).Place);
        Assert.Equal(3, Row(t, b).Place);
    }

    [Fact]
    public void EliminatedPlayersPlaceIsLockedWhileOthersStillPlay()
    {
        var t = MakeChip("A", "B", "C", "D");
        var svc = Service();
        svc.StartChipTournament(t, 1);
        AddTables(t, 1);
        // Seed A and D together at the one table; B and C wait in NextUp.
        t.Entrants[0].SeedNumber = 1; // A
        t.Entrants[3].SeedNumber = 2; // D
        var tableId = t.Tables[0].Id;

        svc.RecordGame(t, tableId, t.Entrants[0].Id, t.Entrants[3].Id); // D out first -> place 4
        Assert.Equal(TournamentStatus.InProgress, t.Status);
        Assert.Equal(4, Row(t, t.Entrants[3]).Place);
        // Remaining players have no final place yet.
        Assert.Null(Row(t, t.Entrants[0]).Place);
        Assert.Null(Row(t, t.Entrants[1]).Place);
    }

    [Fact]
    public void ComputeStandings_TracksWinsLossesAndWinPercentage()
    {
        var t = MakeChip("A", "B", "C");
        var svc = Service();
        svc.StartChipTournament(t, startingChips: 5);
        AddTables(t, 1);
        var tableId = t.Tables[0].Id;
        var a = t.Entrants[0];
        var b = t.Entrants[1];
        var c = t.Entrants[2];

        svc.RecordGame(t, tableId, a.Id, b.Id); // A beats B; C is waiting, fills B's seat
        svc.RecordGame(t, tableId, a.Id, c.Id); // A beats C

        var aRow = Row(t, a);
        Assert.Equal(2, aRow.MatchesWon);
        Assert.Equal(2, aRow.MatchesPlayed);
        Assert.Equal(100, aRow.WinPercentage);

        var bRow = Row(t, b);
        Assert.Equal(0, bRow.MatchesWon);
        Assert.Equal(1, bRow.MatchesPlayed);
        Assert.Equal(0, bRow.WinPercentage);
    }

    [Fact]
    public void ShuffleAndSeatPlayers_AssignsSeedNumbersOnlyToActiveEntrants()
    {
        var t = MakeChip("A", "B", "C", "D");
        var svc = Service();
        svc.StartChipTournament(t, 1);
        t.Entrants[3].IsEliminated = true; // D is somehow already out (shouldn't happen pre-start, but guard it)

        svc.ShuffleAndSeatPlayers(t);

        var active = t.Entrants.Where(e => !e.IsEliminated).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, active.Select(e => e.SeedNumber!.Value).OrderBy(n => n));
        Assert.Null(t.Entrants[3].SeedNumber);
    }

    [Fact]
    public void ShuffleAndSeatPlayers_ThrowsOnceTableRotationHasStarted()
    {
        var t = MakeChip("A", "B");
        var svc = Service();
        svc.StartChipTournament(t, 3);
        AddTables(t, 1);

        svc.RecordGame(t, t.Tables[0].Id, t.Entrants[0].Id, t.Entrants[1].Id);

        var ex = Assert.Throws<InvalidOperationException>(() => svc.ShuffleAndSeatPlayers(t));
        Assert.Contains("already started", ex.Message);
    }

    [Fact]
    public void ShuffleAndSeatPlayers_AllowedAfterLegacyNoTableGames_SoAnOlderTournamentCanAdoptTables()
    {
        var t = MakeChip("A", "B", "C");
        var svc = Service();
        svc.StartChipTournament(t, 3);

        // Simulate games recorded under the old (pre-table) system.
        t.ChipGame!.Entries.Add(new ChipGameEntry
        {
            ChipGameDetailId = t.ChipGame.Id,
            WinnerEntrantId = t.Entrants[0].Id,
            LoserEntrantId = t.Entrants[1].Id,
            Sequence = 0
        });

        svc.ShuffleAndSeatPlayers(t); // should not throw - no TableId-bearing entries yet
        Assert.All(t.Entrants, e => Assert.NotNull(e.SeedNumber));
    }

    [Fact]
    public void ComputeTableBoard_LegacyEntryWithoutTable_AffectsChipsButNotSeating()
    {
        var t = MakeChip("A", "B", "C");
        var svc = Service();
        svc.StartChipTournament(t, 2);
        AddTables(t, 1);
        svc.ShuffleAndSeatPlayers(t);

        t.ChipGame!.Entries.Add(new ChipGameEntry
        {
            ChipGameDetailId = t.ChipGame.Id,
            WinnerEntrantId = t.Entrants[0].Id,
            LoserEntrantId = t.Entrants[1].Id,
            Sequence = 0,
            TableId = null
        });

        var board = ChipGameService.ComputeTableBoard(t);
        // The legacy entry didn't move anyone - initial seeding still holds for all 3 entrants
        // (one table seats 2, the third waits in NextUp).
        Assert.Equal(2, board.Tables[0].Player1 is null ? 0 : 1 + (board.Tables[0].Player2 is null ? 0 : 1));
        Assert.Single(board.NextUp);

        // But the chip loss from the legacy game still counts.
        Assert.Equal(1, Row(t, t.Entrants[1]).ChipsRemaining);
    }

    [Fact]
    public void ComputeTableBoard_InitialSeeding_TwoPerTable_LeftoverToNextUpInOrder()
    {
        var t = MakeChip("P1", "P2", "P3", "P4", "P5");
        var svc = Service();
        svc.StartChipTournament(t, 2);
        AddTables(t, 2);
        svc.ShuffleAndSeatPlayers(t);
        // Pin the shuffle deterministically for this test.
        for (var i = 0; i < 5; i++) t.Entrants[i].SeedNumber = i + 1;

        var board = ChipGameService.ComputeTableBoard(t);

        Assert.Equal(t.Entrants[0].Id, board.Tables[0].Player1!.Id);
        Assert.Equal(t.Entrants[1].Id, board.Tables[0].Player2!.Id);
        Assert.Equal(t.Entrants[2].Id, board.Tables[1].Player1!.Id);
        Assert.Equal(t.Entrants[3].Id, board.Tables[1].Player2!.Id);
        Assert.Equal(new[] { t.Entrants[4].Id }, board.NextUp.Select(e => e.Id));
    }

    [Fact]
    public void ComputeTableBoard_FullWalkthrough_CrossTableRotationAndSinglesConsolidation()
    {
        var t = MakeChip("P1", "P2", "P3", "P4", "P5");
        var svc = Service();
        svc.StartChipTournament(t, startingChips: 2);
        AddTables(t, 2);
        for (var i = 0; i < 5; i++) t.Entrants[i].SeedNumber = i + 1;
        var p = t.Entrants.Select(e => e.Id).ToArray(); // p[0]=P1 ... p[4]=P5
        var t1 = t.Tables[0].Id;
        var t2 = t.Tables[1].Id;

        // Initial: T1=[P1,P2] T2=[P3,P4] NextUp=[P5]

        svc.RecordGame(t, t1, p[0], p[1]); // P1 beats P2 (P2: 2->1, requeues); P5 crosses into T1
        var board = ChipGameService.ComputeTableBoard(t);
        Assert.Equal(p[0], board.Tables[0].Player1!.Id);
        Assert.Equal(p[4], board.Tables[0].Player2!.Id); // P5 filled the vacancy
        Assert.Equal(new[] { p[1] }, board.NextUp.Select(e => e.Id)); // P2 waiting

        svc.RecordGame(t, t2, p[2], p[3]); // P3 beats P4 (P4: 2->1, requeues); P2 crosses into T2
        board = ChipGameService.ComputeTableBoard(t);
        Assert.Equal(p[2], board.Tables[1].Player1!.Id);
        Assert.Equal(p[1], board.Tables[1].Player2!.Id); // P2 (from T1's earlier loss) now at T2
        Assert.Equal(new[] { p[3] }, board.NextUp.Select(e => e.Id)); // P4 waiting

        svc.RecordGame(t, t1, p[0], p[4]); // P1 beats P5 (P5: 2->1, requeues); P4 fills T1
        board = ChipGameService.ComputeTableBoard(t);
        Assert.Equal(p[0], board.Tables[0].Player1!.Id);
        Assert.Equal(p[3], board.Tables[0].Player2!.Id);

        svc.RecordGame(t, t2, p[2], p[1]); // P3 beats P2 (P2: 1->0, ELIMINATED); P5 fills T2
        board = ChipGameService.ComputeTableBoard(t);
        Assert.Equal(p[2], board.Tables[1].Player1!.Id);
        Assert.Equal(p[4], board.Tables[1].Player2!.Id);
        Assert.Empty(board.NextUp);
        Assert.True(t.Entrants[1].IsEliminated); // P2

        svc.RecordGame(t, t1, p[0], p[3]); // P1 beats P4 (P4: 1->0, ELIMINATED); no one to fill T1
        board = ChipGameService.ComputeTableBoard(t);
        Assert.Equal(p[0], board.Tables[0].Player1!.Id);
        Assert.Null(board.Tables[0].Player2); // T1 down to a single, waiting
        Assert.True(t.Entrants[3].IsEliminated); // P4

        svc.RecordGame(t, t2, p[2], p[4]); // P3 beats P5 (P5: 1->0, ELIMINATED)
        // NextUp is empty and both tables are singles -> consolidate onto the earlier table.
        board = ChipGameService.ComputeTableBoard(t);
        Assert.Equal(p[0], board.Tables[0].Player1!.Id);
        Assert.Equal(p[2], board.Tables[0].Player2!.Id);
        Assert.Equal(0, CountSeated(board.Tables[1]));
        Assert.True(t.Entrants[4].IsEliminated); // P5
        Assert.Equal(TournamentStatus.InProgress, t.Status); // P1 and P3 both still active

        svc.RecordGame(t, t1, p[0], p[2]); // P1 beats P3 (P3: 2->1, requeues, then immediately refills T1 - no one else free)
        svc.RecordGame(t, t1, p[0], p[2]); // P1 beats P3 again (P3: 1->0, ELIMINATED) -> P1 champion
        Assert.Equal(TournamentStatus.Completed, t.Status);
        Assert.Equal(p[0], ChipGameService.ComputeStandings(t).First(r => r.Place == 1).Entrant.Id);
    }

    private static int CountSeated(ChipTableSeat seat) =>
        (seat.Player1 is null ? 0 : 1) + (seat.Player2 is null ? 0 : 1);
}
