using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class PrizePayoutServiceTests
{
    private static void SetPrizePlaces(Tournament tournament, params (int Place, decimal Percentage)[] places)
    {
        tournament.PrizePlaces = places.Select(p => new TournamentPrizePlace { TournamentId = tournament.Id, Place = p.Place, Percentage = p.Percentage }).ToList();
    }

    private static decimal PayoutFor(List<PrizePayoutRow> rows, TournamentEntrant entrant) =>
        rows.Single(r => r.Entrant.Id == entrant.Id).Payout;

    [Fact]
    public void MoneyMath_TotalEntryFees_HostCut_PrizePool()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin, EntryFee = 10m, HostFeePercentage = 25m };
        for (var i = 0; i < 4; i++)
        {
            tournament.Entrants.Add(new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid() });
        }

        Assert.Equal(40m, PrizePayoutService.TotalEntryFees(tournament));
        Assert.Equal(10m, PrizePayoutService.HostCut(tournament));
        Assert.Equal(30m, PrizePayoutService.PrizePool(tournament));
    }

    [Fact]
    public void ComputePayouts_ReturnsEmpty_WhenNoPrizePlacesConfigured()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin, EntryFee = 10m };
        tournament.Entrants.Add(new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid() });

        Assert.Empty(PrizePayoutService.ComputePayouts(tournament));
    }

    [Fact]
    public void ComputePayouts_ReturnsEmpty_ForRingGame_EvenIfPrizePlacesSomehowConfigured()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RingGame, EntryFee = 10m };
        tournament.Entrants.Add(new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid() });
        SetPrizePlaces(tournament, (1, 100m));

        Assert.Empty(PrizePayoutService.ComputePayouts(tournament));
    }

    [Fact]
    public void RoundRobin_PlacementIsExact_PayoutsFollowStandingsOrder()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin, EntryFee = 10m };
        var a = new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid(), Player = new Player { FirstName = "A", LastName = "P" } };
        var b = new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid(), Player = new Player { FirstName = "B", LastName = "P" } };
        var c = new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid(), Player = new Player { FirstName = "C", LastName = "P" } };
        tournament.Entrants.AddRange(new[] { a, b, c });

        tournament.Matches.Add(new Match { TournamentId = tournament.Id, Player1EntrantId = a.Id, Player2EntrantId = b.Id, Player1Score = 7, Player2Score = 2, WinnerEntrantId = a.Id, Status = MatchStatus.Completed });
        tournament.Matches.Add(new Match { TournamentId = tournament.Id, Player1EntrantId = a.Id, Player2EntrantId = c.Id, Player1Score = 7, Player2Score = 3, WinnerEntrantId = a.Id, Status = MatchStatus.Completed });
        tournament.Matches.Add(new Match { TournamentId = tournament.Id, Player1EntrantId = b.Id, Player2EntrantId = c.Id, Player1Score = 7, Player2Score = 1, WinnerEntrantId = b.Id, Status = MatchStatus.Completed });

        // Total entry fees = 30, no host cut, pool = 30.
        SetPrizePlaces(tournament, (1, 70m), (2, 20m), (3, 10m));

        var rows = PrizePayoutService.ComputePayouts(tournament);

        Assert.Equal(21m, PayoutFor(rows, a));
        Assert.Equal(6m, PayoutFor(rows, b));
        Assert.Equal(3m, PayoutFor(rows, c));
    }

    [Fact]
    public void ChipTournament_PlacementIsExact_PayoutsFollowEliminationOrder()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.ChipTournament, EntryFee = 10m };
        for (var i = 0; i < 3; i++)
        {
            tournament.Entrants.Add(new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid(), Player = new Player { FirstName = $"P{i}", LastName = "P" } });
        }
        var a = tournament.Entrants[0];
        var b = tournament.Entrants[1];
        var c = tournament.Entrants[2];

        var table = new Table { TournamentId = tournament.Id, Label = "Table 1" };
        tournament.Tables.Add(table);

        var svc = new ChipGameService();
        svc.StartChipTournament(tournament, startingChips: 1);
        svc.RecordGame(tournament, table.Id, a.Id, b.Id); // B out first  -> place 3; C rotates in
        svc.RecordGame(tournament, table.Id, a.Id, c.Id); // C out second -> place 2; A champion -> place 1

        // Total entry fees = 30, no host cut, pool = 30.
        SetPrizePlaces(tournament, (1, 70m), (2, 20m), (3, 10m));

        var rows = PrizePayoutService.ComputePayouts(tournament);

        Assert.Equal(21m, PayoutFor(rows, a));
        Assert.Equal(6m, PayoutFor(rows, c));
        Assert.Equal(3m, PayoutFor(rows, b));
    }

    private static Tournament BuildBracketTournament(TournamentFormat format, int entrantCount)
    {
        var tournament = new Tournament { Name = "Test", Format = format };
        for (var i = 1; i <= entrantCount; i++)
        {
            tournament.Entrants.Add(new TournamentEntrant
            {
                TournamentId = tournament.Id,
                PlayerId = Guid.NewGuid(),
                Player = new Player { FirstName = $"Seed{i}", LastName = "P" },
                SeedNumber = i
            });
        }
        return tournament;
    }

    private static TournamentEntrant BySeed(Tournament tournament, int seed) =>
        tournament.Entrants.First(e => e.SeedNumber == seed);

    private static void Play(BracketGenerationService service, Tournament tournament, int seedA, int seedB, int winnerSeed)
    {
        var a = BySeed(tournament, seedA);
        var b = BySeed(tournament, seedB);
        var match = tournament.Matches.Single(m =>
            m.Status == MatchStatus.Scheduled &&
            ((m.Player1EntrantId == a.Id && m.Player2EntrantId == b.Id) ||
             (m.Player1EntrantId == b.Id && m.Player2EntrantId == a.Id)));

        var winnerIsPlayer1 = match.Player1EntrantId == BySeed(tournament, winnerSeed).Id;
        match.Status = MatchStatus.InProgress;
        service.RecordMatchResult(tournament, match, winnerIsPlayer1 ? 7 : 3, winnerIsPlayer1 ? 3 : 7);
    }

    [Fact]
    public void SingleElimination_ChampionAndRunnerUpAreExact_SemifinalLosersTieForThird()
    {
        var service = new BracketGenerationService();
        var tournament = BuildBracketTournament(TournamentFormat.SingleElimination, 4);
        service.GenerateSingleElimination(tournament);

        // Seed slot order for size 4 is [1,4,2,3]: R1 is seed1 v seed4, seed2 v seed3.
        Play(service, tournament, 1, 4, winnerSeed: 1);
        Play(service, tournament, 2, 3, winnerSeed: 2);
        Play(service, tournament, 1, 2, winnerSeed: 1); // final: champion=1, runner-up=2

        Assert.Equal(TournamentStatus.Completed, tournament.Status);

        tournament.EntryFee = 10m; // 4 entrants -> total 40, no host cut, pool 40
        SetPrizePlaces(tournament, (1, 50m), (2, 30m), (3, 20m)); // no 4th-place cut configured

        var rows = PrizePayoutService.ComputePayouts(tournament);

        Assert.Equal(20m, PayoutFor(rows, BySeed(tournament, 1))); // champion: 50% of 40
        Assert.Equal(12m, PayoutFor(rows, BySeed(tournament, 2))); // runner-up: 30% of 40

        // Seeds 3 and 4 both lost their only match (0W-1L) - tied for 3rd-4th, splitting just
        // the funded 3rd-place cut (20% of 40 = 8) evenly, since no 4th place is configured.
        Assert.Equal(4m, PayoutFor(rows, BySeed(tournament, 3)));
        Assert.Equal(4m, PayoutFor(rows, BySeed(tournament, 4)));

        var seed3Row = rows.Single(r => r.Entrant.Id == BySeed(tournament, 3).Id);
        Assert.Equal(3, seed3Row.PlaceRangeStart);
        Assert.Equal(4, seed3Row.PlaceRangeEnd);
    }

    [Fact]
    public void DoubleElimination_Size4_EveryPlaceIsExact_NoTies()
    {
        var service = new BracketGenerationService();
        var tournament = BuildBracketTournament(TournamentFormat.DoubleElimination, 4);
        service.GenerateDoubleElimination(tournament);

        Play(service, tournament, 1, 4, winnerSeed: 1); // WB R1
        Play(service, tournament, 2, 3, winnerSeed: 2); // WB R1
        Play(service, tournament, 4, 3, winnerSeed: 4); // LB R1: 3 eliminated (4th place)
        Play(service, tournament, 1, 2, winnerSeed: 1); // WB final
        Play(service, tournament, 4, 2, winnerSeed: 4); // LB final: 2 eliminated (3rd place)
        Play(service, tournament, 1, 4, winnerSeed: 1); // Grand Final, WB champ wins outright

        Assert.Equal(TournamentStatus.Completed, tournament.Status);

        tournament.EntryFee = 10m; // 4 entrants -> total 40, no host cut, pool 40
        SetPrizePlaces(tournament, (1, 50m), (2, 30m), (3, 15m), (4, 5m));

        var rows = PrizePayoutService.ComputePayouts(tournament);

        Assert.Equal(20m, PayoutFor(rows, BySeed(tournament, 1))); // champion
        Assert.Equal(12m, PayoutFor(rows, BySeed(tournament, 4))); // runner-up (grand final loser)
        Assert.Equal(6m, PayoutFor(rows, BySeed(tournament, 2)));  // LB final loser: unambiguous 3rd
        Assert.Equal(2m, PayoutFor(rows, BySeed(tournament, 3)));  // LB round 1 loser: unambiguous 4th

        Assert.All(rows, r => Assert.Equal(r.PlaceRangeStart, r.PlaceRangeEnd)); // nobody tied
    }

    [Fact]
    public void ModifiedSingleElimination_Size8_ThreeTiersOfTiedPodExits()
    {
        var service = new BracketGenerationService();
        var tournament = BuildBracketTournament(TournamentFormat.ModifiedSingleElimination, 8);
        service.GenerateModifiedSingleElimination(tournament);

        // Round 1: seed(2i+1) v seed(2i+2) per pod-building order.
        Play(service, tournament, 1, 2, winnerSeed: 1);
        Play(service, tournament, 3, 4, winnerSeed: 3);
        Play(service, tournament, 5, 6, winnerSeed: 5);
        Play(service, tournament, 7, 8, winnerSeed: 7);

        // Losers Round 1 (eliminates outright): lane0 = R1 losers {2,4}; lane1 = {6,8}.
        Play(service, tournament, 2, 4, winnerSeed: 2); // 4 eliminated (worst tier)
        Play(service, tournament, 6, 8, winnerSeed: 6); // 8 eliminated (worst tier)

        // Winners Round 2: lane0 = R1 winners {1,3}; lane1 = {5,7}.
        Play(service, tournament, 1, 3, winnerSeed: 1); // 3 drops to Losers Round 2
        Play(service, tournament, 5, 7, winnerSeed: 5); // 7 drops to Losers Round 2

        // Losers Round 2 ("receiving"): lane0 = {2 (LR1 survivor), 3 (WR2 loser)}; lane1 = {6, 7}.
        Play(service, tournament, 2, 3, winnerSeed: 2); // 3 eliminated (middle tier)
        Play(service, tournament, 6, 7, winnerSeed: 6); // 7 eliminated (middle tier)

        // Final Four: lane0 = {1 (WR2 winner), 2 (LR2 survivor)}; lane1 = {5, 6}.
        Play(service, tournament, 1, 2, winnerSeed: 1); // 2 eliminated (best non-rep tier)
        Play(service, tournament, 5, 6, winnerSeed: 5); // 6 eliminated (best non-rep tier)

        // Final: the pod's 2 reps.
        Play(service, tournament, 1, 5, winnerSeed: 1); // champion=1, runner-up=5

        Assert.Equal(TournamentStatus.Completed, tournament.Status);

        tournament.EntryFee = 10m; // 8 entrants -> total 80, no host cut, pool 80
        SetPrizePlaces(tournament, (1, 60m), (2, 30m), (3, 10m)); // no 4th place and below funded

        var rows = PrizePayoutService.ComputePayouts(tournament);

        Assert.Equal(48m, PayoutFor(rows, BySeed(tournament, 1))); // champion: 60% of 80
        Assert.Equal(24m, PayoutFor(rows, BySeed(tournament, 5))); // runner-up: 30% of 80

        // Final Four losers {2, 6}: both 2W-2L, tied for 3rd-4th, splitting just the funded
        // 3rd-place cut (10% of 80 = 8) since no 4th place is configured.
        Assert.Equal(4m, PayoutFor(rows, BySeed(tournament, 2)));
        Assert.Equal(4m, PayoutFor(rows, BySeed(tournament, 6)));

        // Losers Round 2 losers {3, 7}: both 1W-2L, tied for 5th-6th - unfunded.
        Assert.Equal(0m, PayoutFor(rows, BySeed(tournament, 3)));
        Assert.Equal(0m, PayoutFor(rows, BySeed(tournament, 7)));

        // Losers Round 1 losers {4, 8}: both 0W-2L, tied for 7th-8th (last) - unfunded.
        Assert.Equal(0m, PayoutFor(rows, BySeed(tournament, 4)));
        Assert.Equal(0m, PayoutFor(rows, BySeed(tournament, 8)));
    }

    [Fact]
    public void BracketFormats_ComputePayouts_ReturnsEmpty_UntilTournamentCompletes()
    {
        var service = new BracketGenerationService();
        var tournament = BuildBracketTournament(TournamentFormat.SingleElimination, 4);
        service.GenerateSingleElimination(tournament);
        tournament.EntryFee = 10m;
        SetPrizePlaces(tournament, (1, 100m));

        Assert.Equal(TournamentStatus.NotStarted, tournament.Status);
        Assert.Empty(PrizePayoutService.ComputePayouts(tournament));
    }

    [Fact]
    public void ComputeFinalResults_ListsEveryEntrant_WithZeroPayout_WhenNoPrizePlacesConfigured()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin };
        var a = new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid(), Player = new Player { FirstName = "A", LastName = "P" } };
        var b = new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid(), Player = new Player { FirstName = "B", LastName = "P" } };
        var c = new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid(), Player = new Player { FirstName = "C", LastName = "P" } };
        tournament.Entrants.AddRange(new[] { a, b, c });

        tournament.Matches.Add(new Match { TournamentId = tournament.Id, Player1EntrantId = a.Id, Player2EntrantId = b.Id, Player1Score = 7, Player2Score = 2, WinnerEntrantId = a.Id, Status = MatchStatus.Completed });
        tournament.Matches.Add(new Match { TournamentId = tournament.Id, Player1EntrantId = a.Id, Player2EntrantId = c.Id, Player1Score = 7, Player2Score = 3, WinnerEntrantId = a.Id, Status = MatchStatus.Completed });
        tournament.Matches.Add(new Match { TournamentId = tournament.Id, Player1EntrantId = b.Id, Player2EntrantId = c.Id, Player1Score = 7, Player2Score = 1, WinnerEntrantId = b.Id, Status = MatchStatus.Completed });

        // No prize places configured at all - ComputePayouts is empty, but final placements still resolve.
        Assert.Empty(PrizePayoutService.ComputePayouts(tournament));

        var results = PrizePayoutService.ComputeFinalResults(tournament);

        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0].PlaceRangeStart);
        Assert.Equal(a.Id, results[0].Entrant.Id);
        Assert.Equal(b.Id, results[1].Entrant.Id);
        Assert.Equal(c.Id, results[2].Entrant.Id);
        Assert.All(results, r => Assert.Equal(0m, r.Payout));
    }

    [Fact]
    public void ComputeFinalResults_IncludesUnfundedPlaces_AlongsideFundedOnes()
    {
        var service = new BracketGenerationService();
        var tournament = BuildBracketTournament(TournamentFormat.SingleElimination, 4);
        service.GenerateSingleElimination(tournament);

        Play(service, tournament, 1, 4, winnerSeed: 1);
        Play(service, tournament, 2, 3, winnerSeed: 2);
        Play(service, tournament, 1, 2, winnerSeed: 1);

        tournament.EntryFee = 10m; // pool 40
        SetPrizePlaces(tournament, (1, 100m)); // only 1st is funded

        var results = PrizePayoutService.ComputeFinalResults(tournament);

        // All four entrants placed, even though only the champion earns money.
        Assert.Equal(4, results.Count);
        Assert.Equal(40m, results.Single(r => r.Entrant.Id == BySeed(tournament, 1).Id).Payout);
        Assert.Equal(0m, results.Single(r => r.Entrant.Id == BySeed(tournament, 2).Id).Payout);
        Assert.All(results.Where(r => r.PlaceRangeStart >= 3), r => Assert.Equal(0m, r.Payout));
    }

    [Fact]
    public void ComputeFinalResults_ReturnsEmpty_ForRingGame()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RingGame };
        tournament.Entrants.Add(new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid() });

        Assert.Empty(PrizePayoutService.ComputeFinalResults(tournament));
    }
}
