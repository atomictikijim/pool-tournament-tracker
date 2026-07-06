using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Services;

public class BracketGenerationService
{
    /// <summary>
    /// Builds a single-elimination bracket for the tournament's entrants (which must already
    /// have SeedNumber assigned, e.g. via SeedingService.AssignSeeds). Round-1 byes for
    /// non-power-of-2 entrant counts are resolved immediately.
    /// </summary>
    public BracketDetail GenerateSingleElimination(Tournament tournament)
    {
        var entrants = tournament.Entrants.Count;
        if (entrants < 2)
        {
            throw new InvalidOperationException("A single-elimination bracket requires at least 2 entrants.");
        }

        var entrantsBySeed = tournament.Entrants
            .Where(e => e.SeedNumber is not null)
            .ToDictionary(e => e.SeedNumber!.Value);

        var bracketSize = NextPowerOfTwo(entrants);

        var bracket = new BracketDetail
        {
            TournamentId = tournament.Id,
            IsDoubleElimination = false
        };
        tournament.Bracket = bracket;

        var round1 = BuildWinnersRound1(tournament, bracket, entrantsBySeed, bracketSize);
        BuildWinnersRounds2AndUp(bracket, round1);

        // Now that every node (and its FeedsIntoWinnerNodeId) exists, resolve any round-1 byes,
        // propagating the auto-advanced winner as far as the chain of completed matches allows.
        foreach (var byeNode in bracket.Nodes.Where(n => n.RoundNumber == 1 && n.Match is { IsBye: true }))
        {
            PropagateWinner(tournament, bracket, byeNode, byeNode.Match!.WinnerEntrantId!.Value);
        }

        tournament.Status = TournamentStatus.InProgress;
        return bracket;
    }

    /// <summary>
    /// Builds a double-elimination bracket: a winners bracket identical in shape to
    /// GenerateSingleElimination's, a losers bracket that receives each winners-bracket round's
    /// losers at the correct point (round 1's losers seed the losers bracket directly; every
    /// later round's losers merge in after a "consolidation" round has caught the losers bracket
    /// back down to a matching player count), and a Grand Final between the two bracket champions
    /// with a single bracket-reset rematch if the losers-bracket champion wins it.
    /// Requires an exact power-of-2 entrant count - seeding a non-power-of-2 losers bracket
    /// (byes cascading through both brackets at once) is not yet supported.
    /// </summary>
    public BracketDetail GenerateDoubleElimination(Tournament tournament)
    {
        var entrantCount = tournament.Entrants.Count;
        if (entrantCount < 2)
        {
            throw new InvalidOperationException("A double-elimination bracket requires at least 2 entrants.");
        }

        if ((entrantCount & (entrantCount - 1)) != 0)
        {
            throw new InvalidOperationException(
                "Double elimination currently requires a power-of-2 number of entrants (2, 4, 8, 16, 32...).");
        }

        var entrantsBySeed = tournament.Entrants
            .Where(e => e.SeedNumber is not null)
            .ToDictionary(e => e.SeedNumber!.Value);

        var bracketSize = entrantCount;
        var totalWbRounds = (int)Math.Log2(bracketSize);

        var bracket = new BracketDetail
        {
            TournamentId = tournament.Id,
            IsDoubleElimination = true
        };
        tournament.Bracket = bracket;

        var wbRoundsByNumber = new List<List<BracketNode>>();
        var round1 = BuildWinnersRound1(tournament, bracket, entrantsBySeed, bracketSize);
        wbRoundsByNumber.Add(round1);

        var currentRound = round1;
        for (var r = 1; r < totalWbRounds; r++)
        {
            var nextRound = new List<BracketNode>();
            for (var i = 0; i < currentRound.Count / 2; i++)
            {
                var node = new BracketNode { BracketDetailId = bracket.Id, Side = BracketSide.Winners, RoundNumber = r + 1, PositionInRound = i };
                bracket.Nodes.Add(node);
                nextRound.Add(node);

                currentRound[2 * i].FeedsIntoWinnerNodeId = node.Id;
                currentRound[2 * i + 1].FeedsIntoWinnerNodeId = node.Id;
            }

            wbRoundsByNumber.Add(nextRound);
            currentRound = nextRound;
        }

        var wbFinalNode = wbRoundsByNumber[totalWbRounds - 1][0];

        BracketNode? lbFinalNode = null;
        var lastLbRoundNumber = 0;

        if (totalWbRounds > 1)
        {
            var lbRoundNumber = 1;

            // LB round 1: pair up round-1's losers, position-adjacent.
            var lbSurvivors = new List<BracketNode>();
            for (var i = 0; i < round1.Count / 2; i++)
            {
                var node = new BracketNode { BracketDetailId = bracket.Id, Side = BracketSide.Losers, RoundNumber = lbRoundNumber, PositionInRound = i };
                bracket.Nodes.Add(node);
                lbSurvivors.Add(node);

                round1[2 * i].FeedsIntoLoserNodeId = node.Id;
                round1[2 * i].FeedsIntoLoserSlot = 1;
                round1[2 * i + 1].FeedsIntoLoserNodeId = node.Id;
                round1[2 * i + 1].FeedsIntoLoserSlot = 2;
            }
            lastLbRoundNumber = lbRoundNumber;
            lbRoundNumber++;

            // LB round 2: receive WB round 2's losers directly - counts already match, no
            // consolidation round needed yet.
            var wbRound2 = wbRoundsByNumber[1];
            lbSurvivors = BuildLosersReceivingRound(bracket, ref lbRoundNumber, lbSurvivors, wbRound2);
            lastLbRoundNumber = lbRoundNumber - 1;

            // Every later WB round's losers arrive at half the count of the current LB
            // survivors, so a pure consolidation round must halve the survivors first.
            for (var k = 3; k <= totalWbRounds; k++)
            {
                lbSurvivors = BuildLosersConsolidationRound(bracket, ref lbRoundNumber, lbSurvivors);
                lbSurvivors = BuildLosersReceivingRound(bracket, ref lbRoundNumber, lbSurvivors, wbRoundsByNumber[k - 1]);
                lastLbRoundNumber = lbRoundNumber - 1;
            }

            lbFinalNode = lbSurvivors[0];
        }

        var grandFinalNode = new BracketNode
        {
            BracketDetailId = bracket.Id,
            Side = BracketSide.GrandFinal,
            RoundNumber = Math.Max(totalWbRounds, lastLbRoundNumber) + 1,
            PositionInRound = 0
        };
        bracket.Nodes.Add(grandFinalNode);

        wbFinalNode.FeedsIntoWinnerNodeId = grandFinalNode.Id;
        wbFinalNode.FeedsIntoWinnerSlot = 1;

        if (lbFinalNode is null)
        {
            // Only 2 entrants: the sole round-1 match's loser has no one left to play in the
            // losers bracket, so they arrive at the Grand Final as its "losers champion" untested.
            wbFinalNode.FeedsIntoLoserNodeId = grandFinalNode.Id;
            wbFinalNode.FeedsIntoLoserSlot = 2;
        }
        else
        {
            lbFinalNode.FeedsIntoWinnerNodeId = grandFinalNode.Id;
            lbFinalNode.FeedsIntoWinnerSlot = 2;
        }

        tournament.Status = TournamentStatus.InProgress;
        return bracket;
    }

    /// <summary>
    /// Records a completed match's score, determines the winner, and advances that winner (and,
    /// for a winners-bracket match in a double-elimination tournament, drops the loser into the
    /// losers bracket). Returns every newly-materialized Match this produced (there can be more
    /// than one - the winner and loser can each complete a different downstream node at once) so
    /// the caller's persistence layer can explicitly track them as new entities (they were
    /// attached to an already-tracked Tournament graph, so change-tracking can't infer that).
    /// </summary>
    public IReadOnlyList<Match> RecordMatchResult(Tournament tournament, Match match, int player1Score, int player2Score)
    {
        if (match.Player2EntrantId is null)
        {
            throw new InvalidOperationException("Cannot record a score for a bye match.");
        }

        if (player1Score == player2Score)
        {
            throw new InvalidOperationException("A match cannot end in a tie; one player must win.");
        }

        match.Player1Score = player1Score;
        match.Player2Score = player2Score;
        match.WinnerEntrantId = player1Score > player2Score ? match.Player1EntrantId : match.Player2EntrantId;
        match.Status = MatchStatus.Completed;

        var bracket = tournament.Bracket;
        var node = bracket?.Nodes.FirstOrDefault(n => n.Id == match.BracketNodeId);
        if (bracket is null || node is null)
        {
            return Array.Empty<Match>();
        }

        if (node.Side == BracketSide.GrandFinal)
        {
            if (node.IsGrandFinalReset || match.WinnerEntrantId == match.Player1EntrantId)
            {
                // Either this was already the decider, or the winners-bracket champion (slot 1)
                // won it straight through - either way, the tournament is over.
                tournament.Status = TournamentStatus.Completed;
                return Array.Empty<Match>();
            }

            // The losers-bracket champion (slot 2) beat the previously-undefeated player - both
            // now have exactly one loss, so a single decider match settles the tournament.
            var resetMatch = CreateGrandFinalResetMatch(tournament, bracket, node, match);
            return new[] { resetMatch };
        }

        var newMatches = new List<Match>();

        var winnerMatch = PropagateWinner(tournament, bracket, node, match.WinnerEntrantId.Value);
        if (winnerMatch is not null)
        {
            newMatches.Add(winnerMatch);
        }

        if (node.Side == BracketSide.Winners && node.FeedsIntoLoserNodeId is not null)
        {
            var loserEntrantId = match.WinnerEntrantId == match.Player1EntrantId
                ? match.Player2EntrantId!.Value
                : match.Player1EntrantId;

            var loserMatch = PropagateLoser(tournament, bracket, node, loserEntrantId);
            if (loserMatch is not null)
            {
                newMatches.Add(loserMatch);
            }
        }

        return newMatches;
    }

    private List<BracketNode> BuildWinnersRound1(
        Tournament tournament, BracketDetail bracket, Dictionary<int, TournamentEntrant> entrantsBySeed, int bracketSize)
    {
        var seedSlots = BuildSeedSlotOrder(bracketSize);
        var round1 = new List<BracketNode>();

        for (var i = 0; i < bracketSize / 2; i++)
        {
            var seedA = seedSlots[2 * i];
            var seedB = seedSlots[2 * i + 1];

            var node = new BracketNode
            {
                BracketDetailId = bracket.Id,
                Side = BracketSide.Winners,
                RoundNumber = 1,
                PositionInRound = i,
                Slot1EntrantId = entrantsBySeed.GetValueOrDefault(seedA)?.Id,
                Slot2EntrantId = entrantsBySeed.GetValueOrDefault(seedB)?.Id
            };
            bracket.Nodes.Add(node);
            round1.Add(node);

            MaterializeRound1Match(tournament, node);
        }

        return round1;
    }

    private static void BuildWinnersRounds2AndUp(BracketDetail bracket, List<BracketNode> round1)
    {
        var roundNodes = round1;
        var roundNumber = 1;
        while (roundNodes.Count > 1)
        {
            var nextRoundNodes = new List<BracketNode>();
            for (var i = 0; i < roundNodes.Count / 2; i++)
            {
                var nextNode = new BracketNode
                {
                    BracketDetailId = bracket.Id,
                    Side = BracketSide.Winners,
                    RoundNumber = roundNumber + 1,
                    PositionInRound = i
                };
                bracket.Nodes.Add(nextNode);
                nextRoundNodes.Add(nextNode);

                roundNodes[2 * i].FeedsIntoWinnerNodeId = nextNode.Id;
                roundNodes[2 * i + 1].FeedsIntoWinnerNodeId = nextNode.Id;
            }

            roundNodes = nextRoundNodes;
            roundNumber++;
        }
    }

    /// <summary>
    /// A "receiving" losers-bracket round: each survivor from the previous LB round (slot 1)
    /// is paired, position for position, with that WB round's loser (slot 2).
    /// </summary>
    private static List<BracketNode> BuildLosersReceivingRound(
        BracketDetail bracket, ref int lbRoundNumber, List<BracketNode> survivors, List<BracketNode> wbLoserSources)
    {
        var round = new List<BracketNode>();
        for (var i = 0; i < survivors.Count; i++)
        {
            var node = new BracketNode { BracketDetailId = bracket.Id, Side = BracketSide.Losers, RoundNumber = lbRoundNumber, PositionInRound = i };
            bracket.Nodes.Add(node);
            round.Add(node);

            survivors[i].FeedsIntoWinnerNodeId = node.Id;
            survivors[i].FeedsIntoWinnerSlot = 1;
            wbLoserSources[i].FeedsIntoLoserNodeId = node.Id;
            wbLoserSources[i].FeedsIntoLoserSlot = 2;
        }

        lbRoundNumber++;
        return round;
    }

    /// <summary>
    /// A pure losers-bracket "consolidation" round: survivors from the previous LB round play
    /// each other (position-adjacent), halving the count with no new winners-bracket losers
    /// arriving, so the count matches the next winners-bracket round's losers.
    /// </summary>
    private static List<BracketNode> BuildLosersConsolidationRound(
        BracketDetail bracket, ref int lbRoundNumber, List<BracketNode> survivors)
    {
        var round = new List<BracketNode>();
        for (var i = 0; i < survivors.Count / 2; i++)
        {
            var node = new BracketNode { BracketDetailId = bracket.Id, Side = BracketSide.Losers, RoundNumber = lbRoundNumber, PositionInRound = i };
            bracket.Nodes.Add(node);
            round.Add(node);

            survivors[2 * i].FeedsIntoWinnerNodeId = node.Id;
            survivors[2 * i].FeedsIntoWinnerSlot = 1;
            survivors[2 * i + 1].FeedsIntoWinnerNodeId = node.Id;
            survivors[2 * i + 1].FeedsIntoWinnerSlot = 2;
        }

        lbRoundNumber++;
        return round;
    }

    private Match CreateGrandFinalResetMatch(Tournament tournament, BracketDetail bracket, BracketNode grandFinalNode, Match grandFinalMatch)
    {
        var resetNode = new BracketNode
        {
            BracketDetailId = bracket.Id,
            Side = BracketSide.GrandFinal,
            RoundNumber = grandFinalNode.RoundNumber + 1,
            PositionInRound = 0,
            IsGrandFinalReset = true,
            Slot1EntrantId = grandFinalMatch.Player1EntrantId,
            Slot2EntrantId = grandFinalMatch.Player2EntrantId
        };
        bracket.Nodes.Add(resetNode);

        var resetMatch = new Match
        {
            TournamentId = tournament.Id,
            BracketNodeId = resetNode.Id,
            Player1EntrantId = grandFinalMatch.Player1EntrantId,
            Player2EntrantId = grandFinalMatch.Player2EntrantId,
            Status = MatchStatus.Scheduled
        };
        tournament.Matches.Add(resetMatch);
        resetNode.Match = resetMatch;
        resetNode.MatchId = resetMatch.Id;

        return resetMatch;
    }

    private Match? PropagateWinner(Tournament tournament, BracketDetail bracket, BracketNode completedNode, Guid winnerEntrantId)
    {
        if (completedNode.FeedsIntoWinnerNodeId is null)
        {
            tournament.Status = TournamentStatus.Completed;
            return null;
        }

        var targetNode = bracket.Nodes.First(n => n.Id == completedNode.FeedsIntoWinnerNodeId);
        var slot = completedNode.FeedsIntoWinnerSlot ?? (completedNode.PositionInRound % 2 == 0 ? 1 : 2);
        if (slot == 1)
        {
            targetNode.Slot1EntrantId = winnerEntrantId;
        }
        else
        {
            targetNode.Slot2EntrantId = winnerEntrantId;
        }

        return TryMaterializeAdvancedMatch(tournament, targetNode);
    }

    /// <summary>
    /// Drops a winners-bracket match's loser into its wired losers-bracket (or, for a 2-entrant
    /// bracket, Grand Final) node. A losers-bracket loss is never propagated anywhere further -
    /// there is no FeedsIntoLoserNodeId chain beyond the winners bracket.
    /// </summary>
    private Match? PropagateLoser(Tournament tournament, BracketDetail bracket, BracketNode completedWbNode, Guid loserEntrantId)
    {
        var targetNode = bracket.Nodes.First(n => n.Id == completedWbNode.FeedsIntoLoserNodeId);
        var slot = completedWbNode.FeedsIntoLoserSlot ?? 2;
        if (slot == 1)
        {
            targetNode.Slot1EntrantId = loserEntrantId;
        }
        else
        {
            targetNode.Slot2EntrantId = loserEntrantId;
        }

        return TryMaterializeAdvancedMatch(tournament, targetNode);
    }

    /// <summary>
    /// Round 1 has complete information immediately: a node's second slot being empty means
    /// the opponent seed doesn't exist (a permanent bye), not that it's merely pending.
    /// </summary>
    private void MaterializeRound1Match(Tournament tournament, BracketNode node)
    {
        if (node.Slot1EntrantId is null)
        {
            return;
        }

        var match = node.Slot2EntrantId is null
            ? new Match
            {
                TournamentId = tournament.Id,
                BracketNodeId = node.Id,
                Player1EntrantId = node.Slot1EntrantId.Value,
                Player2EntrantId = null,
                WinnerEntrantId = node.Slot1EntrantId.Value,
                Status = MatchStatus.Completed
            }
            : new Match
            {
                TournamentId = tournament.Id,
                BracketNodeId = node.Id,
                Player1EntrantId = node.Slot1EntrantId.Value,
                Player2EntrantId = node.Slot2EntrantId.Value,
                Status = MatchStatus.Scheduled
            };

        tournament.Matches.Add(match);
        node.Match = match;
        node.MatchId = match.Id;
    }

    /// <summary>
    /// Round 2+ (and losers-bracket) nodes must never auto-complete as a bye: an empty slot here
    /// always means "the other feeder hasn't been played yet", not "no opponent will ever arrive".
    /// </summary>
    private Match? TryMaterializeAdvancedMatch(Tournament tournament, BracketNode node)
    {
        if (node.MatchId is not null || node.Slot1EntrantId is null || node.Slot2EntrantId is null)
        {
            return null;
        }

        var match = new Match
        {
            TournamentId = tournament.Id,
            BracketNodeId = node.Id,
            Player1EntrantId = node.Slot1EntrantId.Value,
            Player2EntrantId = node.Slot2EntrantId.Value,
            Status = MatchStatus.Scheduled
        };

        tournament.Matches.Add(match);
        node.Match = match;
        node.MatchId = match.Id;
        return match;
    }

    private static int NextPowerOfTwo(int n)
    {
        var size = 1;
        while (size < n)
        {
            size *= 2;
        }
        return size;
    }

    /// <summary>
    /// Standard recursive tournament seeding order: for a bracket of the given size, returns
    /// the seed number occupying each slot position so that seed 1 and 2 can only meet in the
    /// final, seeds 1-4 can't meet before the semifinal, etc.
    /// </summary>
    private static List<int> BuildSeedSlotOrder(int bracketSize)
    {
        if (bracketSize == 1)
        {
            return new List<int> { 1 };
        }

        var half = BuildSeedSlotOrder(bracketSize / 2);
        var result = new List<int>(bracketSize);
        foreach (var seed in half)
        {
            result.Add(seed);
            result.Add(bracketSize + 1 - seed);
        }

        return result;
    }
}
