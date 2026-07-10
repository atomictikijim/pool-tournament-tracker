using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

/// <summary>
/// Skill-range starting chips (per-player chips by rating) and the tournament director's
/// mid-tournament add/remove-chip adjustments.
/// </summary>
public class ChipSkillChipsAndAdjustmentsTests
{
    private static ChipGameService Service() => new();

    private static ChipStandingRow Row(Tournament t, TournamentEntrant e) =>
        ChipGameService.ComputeStandings(t).First(r => r.Entrant.Id == e.Id);

    private static TournamentEntrant AddPlayer(Tournament t, string name, int? fargo = null, string? tap = null)
    {
        var entrant = new TournamentEntrant
        {
            TournamentId = t.Id,
            PlayerId = Guid.NewGuid(),
            Player = new Player { FirstName = name, LastName = name, FargoRate = fargo, TapRating = tap }
        };
        t.Entrants.Add(entrant);
        return entrant;
    }

    private static Tournament NewChip() => new() { Name = "Chips", Format = TournamentFormat.ChipTournament };

    // The example from the feature request: Fargo 650+ → 3, 550-649 → 4, 450-549 → 5, under 450 → 6.
    private static List<ChipStartingRule> ExampleFargoRules() => new()
    {
        new ChipStartingRule { MinRating = 650, MaxRating = null, Chips = 3 },
        new ChipStartingRule { MinRating = 550, MaxRating = 649, Chips = 4 },
        new ChipStartingRule { MinRating = 450, MaxRating = 549, Chips = 5 },
        new ChipStartingRule { MinRating = null, MaxRating = 449, Chips = 6 },
    };

    [Fact]
    public void SkillRanges_AssignChipsPerPlayerByRating_WithFallbackForUnrated()
    {
        var t = NewChip();
        var top = AddPlayer(t, "Top", fargo: 700);      // 650+  -> 3
        var high = AddPlayer(t, "High", fargo: 600);     // 550-649 -> 4
        var mid = AddPlayer(t, "Mid", fargo: 500);       // 450-549 -> 5
        var low = AddPlayer(t, "Low", fargo: 400);       // under 450 -> 6
        var unrated = AddPlayer(t, "Unrated");           // no Fargo -> fallback (default 9)

        Service().StartChipTournament(t, startingChips: 9, RatingSystem.Fargo, ExampleFargoRules());

        Assert.Equal(3, top.StartingChips);
        Assert.Equal(4, high.StartingChips);
        Assert.Equal(5, mid.StartingChips);
        Assert.Equal(6, low.StartingChips);
        Assert.Equal(9, unrated.StartingChips); // fallback to the flat default

        Assert.Equal(3, Row(t, top).ChipsRemaining);
        Assert.Equal(6, Row(t, low).ChipsRemaining);
        Assert.Equal(9, Row(t, unrated).ChipsRemaining);
    }

    [Fact]
    public void SkillRanges_RatingOutsideEveryRange_FallsBackToDefault()
    {
        var t = NewChip();
        var inRange = AddPlayer(t, "In", fargo: 550);
        var outOfRange = AddPlayer(t, "Out", fargo: 300);
        var rules = new List<ChipStartingRule> { new() { MinRating = 500, MaxRating = 600, Chips = 4 } };

        Service().StartChipTournament(t, startingChips: 7, RatingSystem.Fargo, rules);

        Assert.Equal(4, inRange.StartingChips);
        Assert.Equal(7, outOfRange.StartingChips); // no matching range -> default
    }

    [Fact]
    public void SkillRanges_UseTapRating_ParsedFromString()
    {
        var t = NewChip();
        var strong = AddPlayer(t, "Strong", tap: "7");
        var weak = AddPlayer(t, "Weak", tap: "3");
        var rules = new List<ChipStartingRule>
        {
            new() { MinRating = 6, MaxRating = null, Chips = 2 },
            new() { MinRating = null, MaxRating = 5, Chips = 5 },
        };

        Service().StartChipTournament(t, startingChips: 4, RatingSystem.Tap, rules);

        Assert.Equal(2, strong.StartingChips);
        Assert.Equal(5, weak.StartingChips);
    }

    [Fact]
    public void StartChipTournament_RejectsSkillRangesWithNoRules_OrZeroChipRule()
    {
        var t1 = NewChip(); AddPlayer(t1, "A"); AddPlayer(t1, "B");
        Assert.Throws<InvalidOperationException>(() =>
            Service().StartChipTournament(t1, 3, RatingSystem.Fargo, new List<ChipStartingRule>()));

        var t2 = NewChip(); AddPlayer(t2, "A"); AddPlayer(t2, "B");
        Assert.Throws<InvalidOperationException>(() =>
            Service().StartChipTournament(t2, 3, RatingSystem.Fargo, new List<ChipStartingRule> { new() { Chips = 0 } }));
    }

    [Fact]
    public void AdjustChips_AddAndRemove_ChangeTheCount()
    {
        var t = NewChip();
        var a = AddPlayer(t, "A");
        var b = AddPlayer(t, "B");
        var svc = Service();
        svc.StartChipTournament(t, 3);

        svc.AdjustChips(t, a.Id, +2);
        Assert.Equal(5, Row(t, a).ChipsRemaining);

        svc.AdjustChips(t, a.Id, -1);
        Assert.Equal(4, Row(t, a).ChipsRemaining);
        Assert.Equal(3, Row(t, b).ChipsRemaining); // untouched
    }

    [Fact]
    public void AdjustChips_CannotRemoveMoreChipsThanThePlayerHas()
    {
        var t = NewChip();
        var a = AddPlayer(t, "A");
        AddPlayer(t, "B");
        var svc = Service();
        svc.StartChipTournament(t, 3);

        Assert.Throws<InvalidOperationException>(() => svc.AdjustChips(t, a.Id, -4));
    }

    [Fact]
    public void AdjustChips_PenaltyToZero_EliminatesButKeepsAPlace_WhenOthersRemain()
    {
        var t = NewChip();
        AddPlayer(t, "A");
        AddPlayer(t, "B");
        var c = AddPlayer(t, "C");
        var svc = Service();
        svc.StartChipTournament(t, 3);

        svc.AdjustChips(t, c.Id, -3); // penalize C out of the tournament

        Assert.True(c.IsEliminated);
        Assert.NotNull(Row(t, c).Place);              // still gets a finishing place (no crash)
        Assert.Equal(TournamentStatus.InProgress, t.Status); // A and B still standing
    }

    [Fact]
    public void AdjustChips_AddingAChip_RevivesAnEliminatedPlayer_WhileInProgress()
    {
        var t = NewChip();
        AddPlayer(t, "A");
        AddPlayer(t, "B");
        var c = AddPlayer(t, "C");
        var svc = Service();
        svc.StartChipTournament(t, 3);

        svc.AdjustChips(t, c.Id, -3); // C out
        Assert.True(c.IsEliminated);

        svc.AdjustChips(t, c.Id, +2); // director lets C buy back in
        Assert.False(c.IsEliminated);
        Assert.Equal(2, Row(t, c).ChipsRemaining);
        Assert.Equal(TournamentStatus.InProgress, t.Status);
    }

    [Fact]
    public void AdjustChips_TakingTheSecondToLastPlayerToZero_CompletesTheTournament()
    {
        var t = NewChip();
        var a = AddPlayer(t, "A");
        var b = AddPlayer(t, "B");
        var svc = Service();
        svc.StartChipTournament(t, 3);

        svc.AdjustChips(t, b.Id, -3);

        Assert.Equal(TournamentStatus.Completed, t.Status);
        Assert.Equal(1, Row(t, a).Place);
        // Adjustments are rejected once the tournament has finished.
        Assert.Throws<InvalidOperationException>(() => svc.AdjustChips(t, a.Id, +1));
    }
}
