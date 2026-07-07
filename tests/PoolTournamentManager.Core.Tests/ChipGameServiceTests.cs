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

    private static ChipGameService Service() => new();

    private static ChipStandingRow Row(Tournament t, TournamentEntrant e) =>
        ChipGameService.ComputeStandings(t).First(r => r.Entrant.Id == e.Id);

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

        svc.RecordGame(t, winnerId: t.Entrants[0].Id, loserId: t.Entrants[1].Id);

        Assert.Equal(3, Row(t, t.Entrants[0]).ChipsRemaining); // winner unchanged (lives rule)
        Assert.Equal(2, Row(t, t.Entrants[1]).ChipsRemaining); // loser down one
    }

    [Fact]
    public void RecordGame_RejectsSamePlayer_AndEliminatedPlayer()
    {
        var t = MakeChip("A", "B");
        var svc = Service();
        svc.StartChipTournament(t, 1);
        var a = t.Entrants[0].Id;
        var b = t.Entrants[1].Id;

        Assert.Throws<InvalidOperationException>(() => svc.RecordGame(t, a, a));

        svc.RecordGame(t, a, b); // B loses its only chip -> eliminated, tournament completes
        Assert.Throws<InvalidOperationException>(() => svc.RecordGame(t, a, b)); // B is out
    }

    [Fact]
    public void PlayerIsEliminatedAtZeroChips_AndTournamentCompletesWithChampionFirst()
    {
        var t = MakeChip("A", "B");
        var svc = Service();
        svc.StartChipTournament(t, 2);
        var a = t.Entrants[0];
        var b = t.Entrants[1];

        svc.RecordGame(t, a.Id, b.Id); // B: 2 -> 1
        Assert.False(b.IsEliminated);
        Assert.Equal(TournamentStatus.InProgress, t.Status);

        svc.RecordGame(t, a.Id, b.Id); // B: 1 -> 0, eliminated
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
        var a = t.Entrants[0];
        var b = t.Entrants[1];
        var c = t.Entrants[2];

        svc.RecordGame(t, a.Id, b.Id); // B out first  -> place 3 (last)
        svc.RecordGame(t, a.Id, c.Id); // C out second -> place 2; A champion -> place 1
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

        svc.RecordGame(t, t.Entrants[0].Id, t.Entrants[3].Id); // D out first -> place 4
        Assert.Equal(TournamentStatus.InProgress, t.Status);
        Assert.Equal(4, Row(t, t.Entrants[3]).Place);
        // Remaining players have no final place yet.
        Assert.Null(Row(t, t.Entrants[0]).Place);
        Assert.Null(Row(t, t.Entrants[1]).Place);
    }
}
