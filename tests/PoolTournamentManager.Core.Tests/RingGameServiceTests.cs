using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class RingGameServiceTests
{
    private static Tournament MakeRing(params string[] names)
    {
        var tournament = new Tournament { Name = "Ring", Format = TournamentFormat.RingGame };
        foreach (var name in names)
        {
            tournament.Entrants.Add(new TournamentEntrant
            {
                TournamentId = tournament.Id,
                PlayerId = Guid.NewGuid(),
                Player = new Player { FirstName = name, LastName = "P" }
            });
        }
        return tournament;
    }

    private static RingGameService Service() => new();

    private static RingStandingRow Row(Tournament t, TournamentEntrant e) =>
        RingGameService.ComputeStandings(t).First(r => r.Entrant.Id == e.Id);

    [Fact]
    public void StartRingGame_ChargesBuyIns_AssignsRotation_SeatsFirstShooter()
    {
        var t = MakeRing("A", "B", "C");
        var svc = Service();

        var detail = svc.StartRingGame(t, RingGameType.NineBall, buyInAmount: 20m, fiveBallPayout: 5m, nineBallPayout: 10m);

        Assert.Equal(TournamentStatus.InProgress, t.Status);
        Assert.Equal(1, t.Entrants[0].SeedNumber);
        Assert.Equal(2, t.Entrants[1].SeedNumber);
        Assert.Equal(3, t.Entrants[2].SeedNumber);
        Assert.Equal(t.Entrants[0].Id, detail.CurrentShooterEntrantId);
        Assert.Equal(3, detail.LedgerEntries.Count(l => l.Type == RingLedgerEntryType.BuyIn));
        // Every player starts down their buy-in; pot holds all three buy-ins.
        Assert.Equal(-20m, Row(t, t.Entrants[0]).Net);
        Assert.Equal(60m, RingGameService.PotRemaining(t));
    }

    [Fact]
    public void StartRingGame_RejectsFewerThanTwoPlayers()
    {
        var t = MakeRing("Solo");
        Assert.Throws<InvalidOperationException>(() => Service().StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m));
    }

    [Fact]
    public void MadeTheFive_PaysOut_ButSameShooterKeepsTheTable()
    {
        var t = MakeRing("A", "B");
        var svc = Service();
        var detail = svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m);
        var a = t.Entrants[0];

        svc.RecordMoneyBall(t, a.Id, RingMoneyBall.Five);

        Assert.Equal(a.Id, detail.CurrentShooterEntrantId); // still A's table
        Assert.Equal(1, detail.CurrentRackNumber);          // rack not over
        Assert.Equal(5m, Row(t, a).Winnings);
        Assert.Equal(-15m, Row(t, a).Net);                  // -20 buy-in + 5
        Assert.Equal(35m, RingGameService.PotRemaining(t)); // 40 - 5
    }

    [Fact]
    public void MadeTheNine_PaysOut_EndsRack_AndRotatesBreak()
    {
        var t = MakeRing("A", "B", "C");
        var svc = Service();
        var detail = svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m);
        var a = t.Entrants[0];
        var b = t.Entrants[1];

        svc.RecordMoneyBall(t, a.Id, RingMoneyBall.Nine);

        Assert.Equal(2, detail.CurrentRackNumber);          // new rack
        Assert.Equal(b.Id, detail.CurrentShooterEntrantId); // break rotated to next
        Assert.Equal(10m, Row(t, a).Winnings);
        Assert.Equal(-10m, Row(t, a).Net);
    }

    [Fact]
    public void AdvanceShooter_PassesTurnToNextInRotation_AndWraps()
    {
        var t = MakeRing("A", "B", "C");
        var svc = Service();
        var detail = svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m);

        svc.AdvanceShooter(t);
        Assert.Equal(t.Entrants[1].Id, detail.CurrentShooterEntrantId);
        svc.AdvanceShooter(t);
        Assert.Equal(t.Entrants[2].Id, detail.CurrentShooterEntrantId);
        svc.AdvanceShooter(t);
        Assert.Equal(t.Entrants[0].Id, detail.CurrentShooterEntrantId); // wrapped
    }

    [Fact]
    public void Rotation_SkipsCashedOutPlayers()
    {
        var t = MakeRing("A", "B", "C");
        var svc = Service();
        var detail = svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m);
        var b = t.Entrants[1];

        svc.CashOut(t, b.Id); // B leaves the ring

        // A is current; passing the turn should skip B straight to C.
        svc.AdvanceShooter(t);
        Assert.Equal(t.Entrants[2].Id, detail.CurrentShooterEntrantId);
    }

    [Fact]
    public void CashOut_WhileItIsYourTurn_HandsTurnToNextActivePlayer()
    {
        var t = MakeRing("A", "B", "C");
        var svc = Service();
        var detail = svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m); // A is up

        svc.CashOut(t, t.Entrants[0].Id);

        Assert.True(t.Entrants[0].IsEliminated);
        Assert.Equal(t.Entrants[1].Id, detail.CurrentShooterEntrantId);
    }

    [Fact]
    public void CashOut_RecordsRealizedNet_AndKeepsHistory()
    {
        var t = MakeRing("A", "B");
        var svc = Service();
        svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m);
        var a = t.Entrants[0];
        svc.RecordMoneyBall(t, a.Id, RingMoneyBall.Nine); // A: -20 + 10 = -10

        var marker = svc.CashOut(t, a.Id);

        Assert.Equal(RingLedgerEntryType.CashOut, marker.Type);
        Assert.Equal(-10m, marker.Amount);          // realized net stamped on the marker
        Assert.Equal(-10m, Row(t, a).Net);          // buy-in + winnings history intact
        Assert.True(Row(t, a).IsCashedOut);
    }

    [Fact]
    public void CashOut_DownToOnePlayer_CompletesTournament()
    {
        var t = MakeRing("A", "B");
        var svc = Service();
        var detail = svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m);

        svc.CashOut(t, t.Entrants[0].Id);

        Assert.Equal(TournamentStatus.Completed, t.Status);
        Assert.Null(detail.CurrentShooterEntrantId);
        Assert.Throws<InvalidOperationException>(() => svc.RecordMoneyBall(t, t.Entrants[1].Id, RingMoneyBall.Nine));
    }

    [Fact]
    public void MoneyBall_ByACashedOutPlayer_IsRejected()
    {
        var t = MakeRing("A", "B", "C");
        var svc = Service();
        svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m);
        svc.CashOut(t, t.Entrants[1].Id);

        Assert.Throws<InvalidOperationException>(() => svc.RecordMoneyBall(t, t.Entrants[1].Id, RingMoneyBall.Five));
    }

    [Fact]
    public void Standings_OrderByNetDescending_AndConserveMoney()
    {
        var t = MakeRing("A", "B", "C");
        var svc = Service();
        svc.StartRingGame(t, RingGameType.NineBall, 20m, 5m, 10m);
        var a = t.Entrants[0];
        var c = t.Entrants[2];
        svc.RecordMoneyBall(t, a.Id, RingMoneyBall.Nine); // A +10
        svc.RecordMoneyBall(t, c.Id, RingMoneyBall.Five); // C +5

        var standings = RingGameService.ComputeStandings(t);

        Assert.Equal(a.Id, standings[0].Entrant.Id);   // -10 net, best
        Assert.Equal(c.Id, standings[1].Entrant.Id);   // -15 net
        // Sum of all nets equals the negative of the pot still on the table (money conserved).
        Assert.Equal(-RingGameService.PotRemaining(t), standings.Sum(s => s.Net));
    }

    [Fact]
    public void RecordMoneyBall_WhenPotDepletedToZero_CompletesTournament()
    {
        var t = MakeRing("A", "B");
        var svc = Service();
        var detail = svc.StartRingGame(t, RingGameType.NineBall, 20m, 20m, 20m);
        var a = t.Entrants[0];
        var b = t.Entrants[1];

        // A pockets the 5: pot goes from 40 to 20
        svc.RecordMoneyBall(t, a.Id, RingMoneyBall.Five);
        Assert.Equal(TournamentStatus.InProgress, t.Status);
        Assert.Equal(20m, RingGameService.PotRemaining(t));

        // B pockets the 9: pot goes from 20 to 0 - tournament should end
        svc.RecordMoneyBall(t, b.Id, RingMoneyBall.Nine);

        Assert.Equal(TournamentStatus.Completed, t.Status);
        Assert.Null(detail.CurrentShooterEntrantId);
        Assert.Equal(0m, RingGameService.PotRemaining(t));
    }
}
