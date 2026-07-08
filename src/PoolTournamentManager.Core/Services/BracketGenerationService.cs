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
            Kind = BracketKind.SingleElimination
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
    /// Any entrant count >= 2 is supported: the bracket is padded to the next power of two and the
    /// top seeds receive first-round byes, which cascade into the losers bracket as byes (a
    /// winners-bracket bye produces no loser to drop down, so that losers-bracket slot is itself a
    /// bye - see the bye-resolution pass below and <see cref="AdvanceInto"/>).
    /// </summary>
    public BracketDetail GenerateDoubleElimination(Tournament tournament)
    {
        var entrantCount = tournament.Entrants.Count;
        if (entrantCount < 2)
        {
            throw new InvalidOperationException("A double-elimination bracket requires at least 2 entrants.");
        }

        var entrantsBySeed = tournament.Entrants
            .Where(e => e.SeedNumber is not null)
            .ToDictionary(e => e.SeedNumber!.Value);

        var bracketSize = NextPowerOfTwo(entrantCount);
        var totalWbRounds = (int)Math.Log2(bracketSize);

        var bracket = new BracketDetail
        {
            TournamentId = tournament.Id,
            Kind = BracketKind.DoubleElimination
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

        // Resolve first-round byes now that the whole graph (and every FeedsInto* link) exists:
        // advance each bye winner through the winners bracket, and drop a "bye" into the losers
        // bracket where that match's (non-existent) loser would have gone. AdvanceInto cascades
        // this - two byes meeting in the losers bracket collapse to a phantom that passes the bye on.
        foreach (var byeNode in bracket.Nodes.Where(n => n.Side == BracketSide.Winners && n.RoundNumber == 1 && n.Match is { IsBye: true }).ToList())
        {
            PropagateWinner(tournament, bracket, byeNode, byeNode.Match!.WinnerEntrantId!.Value);
            if (byeNode.FeedsIntoLoserNodeId is not null)
            {
                PropagateLoserBye(tournament, bracket, byeNode);
            }
        }

        tournament.Status = TournamentStatus.InProgress;
        return bracket;
    }

    private const int PodSize = 8;

    /// <summary>
    /// True for entrant counts this format currently supports: a multiple of 8 that's also a
    /// power of 2 (8, 16, 32, 64...), so pods are always full-size and the rep count feeding the
    /// final single-elimination stage is always a clean power of 2. Partial pods / byes are a
    /// known gap for later, same as double elimination's power-of-2-only restriction.
    /// </summary>
    public static bool IsValidModifiedSingleEliminationCount(int entrantCount) =>
        entrantCount >= PodSize && (entrantCount & (entrantCount - 1)) == 0;

    /// <summary>
    /// Builds an APA-style Modified Single Elimination bracket: entrants are split into pods of
    /// 8, each pod running a shortened ladder where round-1 losers get exactly one consolation
    /// match (a second loss there eliminates them) and round-2 losers get one more chance against
    /// those consolation survivors - producing exactly 2 "reps" per pod. Every pod's reps then
    /// feed one ordinary single-elimination bracket with no further consolation chances. Round 1
    /// is a random draw (not a rating seed) - SeedNumber is assigned from that draw order.
    /// </summary>
    public BracketDetail GenerateModifiedSingleElimination(Tournament tournament)
    {
        var entrantCount = tournament.Entrants.Count;
        if (!IsValidModifiedSingleEliminationCount(entrantCount))
        {
            throw new InvalidOperationException(
                "Modified Single Elimination currently requires an entrant count that's a multiple of 8 and a power of 2 (8, 16, 32, 64...).");
        }

        var drawn = RandomDraw(tournament.Entrants);

        var bracket = new BracketDetail
        {
            TournamentId = tournament.Id,
            Kind = BracketKind.ModifiedSingleElimination
        };
        tournament.Bracket = bracket;

        var podCount = entrantCount / PodSize;
        var podReps = new List<BracketNode>();
        for (var p = 0; p < podCount; p++)
        {
            var podEntrants = drawn.GetRange(p * PodSize, PodSize);
            var (rep0, rep1) = BuildModifiedEliminationPod(tournament, bracket, podEntrants, p);
            podReps.Add(rep0);
            podReps.Add(rep1);
        }

        // Interleave so the very first cross-pod round doesn't immediately rematch two reps
        // from the same pod: [pod0.rep0, pod1.rep0, ..., pod0.rep1, pod1.rep1, ...].
        var interleaved = new List<BracketNode>();
        for (var i = 0; i < 2; i++)
        {
            for (var p = 0; p < podCount; p++)
            {
                interleaved.Add(podReps[p * 2 + i]);
            }
        }

        BuildWinnersRounds2AndUp(bracket, interleaved, BracketSide.Final);

        // BuildWinnersRounds2AndUp's target-slot inference falls back to the completed node's own
        // PositionInRound parity, which is only unambiguous within a single freshly-built round
        // (0, 1, 2...). The interleaved list's nodes each carry their pod-relative PositionInRound
        // instead (e.g. two different pods' "lane 0" reps can both be even), so their slot must be
        // set explicitly here from the interleaved list's own index.
        for (var i = 0; i < interleaved.Count; i++)
        {
            interleaved[i].FeedsIntoWinnerSlot = i % 2 == 0 ? 1 : 2;
        }

        tournament.Status = TournamentStatus.InProgress;
        return bracket;
    }

    private static List<TournamentEntrant> RandomDraw(List<TournamentEntrant> entrants)
    {
        var shuffled = entrants.OrderBy(_ => Random.Shared.Next()).ToList();
        for (var i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].SeedNumber = i + 1;
        }
        return shuffled;
    }

    /// <summary>
    /// Builds one 8-entrant pod's Round 1 -> Losers Round 1 (eliminates) -> Winners Round 2 ->
    /// Losers Round 2 (receiving) -> Final Four, and returns the pod's 2 Final-Four nodes (its
    /// "reps" once their winners are known). PositionInRound is offset by podIndex so every pod's
    /// same-named round shares one rendered column instead of colliding at position 0/1.
    /// </summary>
    private (BracketNode Rep0, BracketNode Rep1) BuildModifiedEliminationPod(
        Tournament tournament, BracketDetail bracket, List<TournamentEntrant> podEntrants, int podIndex)
    {
        const int lanesPerPod = PodSize / 4; // 2 matches per pod at every post-round-1 stage

        var round1 = new List<BracketNode>();
        for (var i = 0; i < PodSize / 2; i++)
        {
            var node = new BracketNode
            {
                BracketDetailId = bracket.Id,
                Side = BracketSide.Winners,
                RoundNumber = 1,
                PositionInRound = podIndex * (PodSize / 2) + i,
                Slot1EntrantId = podEntrants[2 * i].Id,
                Slot2EntrantId = podEntrants[2 * i + 1].Id
            };
            bracket.Nodes.Add(node);
            round1.Add(node);
            MaterializeRound1Match(tournament, node);
        }

        // Losers Round 1: pairs Round 1's losers. No FeedsIntoLoserNodeId is wired on these
        // Losers-side nodes, so RecordMatchResult's existing logic (which only drops a loser
        // further when node.Side == Winners) correctly eliminates their losers outright.
        var lbRound1 = new List<BracketNode>();
        for (var i = 0; i < lanesPerPod; i++)
        {
            var node = new BracketNode
            {
                BracketDetailId = bracket.Id,
                Side = BracketSide.Losers,
                RoundNumber = 1,
                PositionInRound = podIndex * lanesPerPod + i
            };
            bracket.Nodes.Add(node);
            lbRound1.Add(node);

            round1[2 * i].FeedsIntoLoserNodeId = node.Id;
            round1[2 * i].FeedsIntoLoserSlot = 1;
            round1[2 * i + 1].FeedsIntoLoserNodeId = node.Id;
            round1[2 * i + 1].FeedsIntoLoserSlot = 2;
        }

        // Winners Round 2: pairs Round 1's winners (still undefeated).
        var wbRound2 = new List<BracketNode>();
        for (var i = 0; i < lanesPerPod; i++)
        {
            var node = new BracketNode
            {
                BracketDetailId = bracket.Id,
                Side = BracketSide.Winners,
                RoundNumber = 2,
                PositionInRound = podIndex * lanesPerPod + i
            };
            bracket.Nodes.Add(node);
            wbRound2.Add(node);

            round1[2 * i].FeedsIntoWinnerNodeId = node.Id;
            round1[2 * i + 1].FeedsIntoWinnerNodeId = node.Id;
        }

        // Losers Round 2 ("receiving"): pairs Losers-Round-1 survivors with Winners-Round-2
        // losers - reuses the double-elimination "receiving round" wiring as-is, then fixes up
        // PositionInRound (the helper numbers from 0, which would collide across pods).
        var lbRoundNumber = 2;
        var lbRound2 = BuildLosersReceivingRound(bracket, ref lbRoundNumber, lbRound1, wbRound2);
        for (var i = 0; i < lbRound2.Count; i++)
        {
            lbRound2[i].PositionInRound = podIndex * lanesPerPod + i;
        }

        // Final Four: pairs each Winners-Round-2 winner (undefeated) with the Losers-Round-2
        // survivor from the same lane (one loss). From here on it's single elimination.
        var finalFour = new List<BracketNode>();
        for (var i = 0; i < lanesPerPod; i++)
        {
            var node = new BracketNode
            {
                BracketDetailId = bracket.Id,
                Side = BracketSide.Final,
                RoundNumber = 1,
                PositionInRound = podIndex * lanesPerPod + i
            };
            bracket.Nodes.Add(node);
            finalFour.Add(node);

            wbRound2[i].FeedsIntoWinnerNodeId = node.Id;
            wbRound2[i].FeedsIntoWinnerSlot = 1;
            lbRound2[i].FeedsIntoWinnerNodeId = node.Id;
            lbRound2[i].FeedsIntoWinnerSlot = 2;
        }

        return (finalFour[0], finalFour[1]);
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

        if (match.Status != MatchStatus.InProgress)
        {
            throw new InvalidOperationException("Start the match before finishing it.");
        }

        if (player1Score == player2Score)
        {
            throw new InvalidOperationException("A match cannot end in a tie; one player must win.");
        }

        match.Player1Score = player1Score;
        match.Player2Score = player2Score;
        match.WinnerEntrantId = player1Score > player2Score ? match.Player1EntrantId : match.Player2EntrantId;
        match.Status = MatchStatus.Completed;
        match.FinishedAtUtc = DateTime.UtcNow;

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

        newMatches.AddRange(PropagateWinner(tournament, bracket, node, match.WinnerEntrantId.Value));

        if (node.Side == BracketSide.Winners && node.FeedsIntoLoserNodeId is not null)
        {
            var loserEntrantId = match.WinnerEntrantId == match.Player1EntrantId
                ? match.Player2EntrantId!.Value
                : match.Player1EntrantId;

            newMatches.AddRange(PropagateLoser(tournament, bracket, node, loserEntrantId));
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
            var entrantA = entrantsBySeed.GetValueOrDefault(seedA);
            var entrantB = entrantsBySeed.GetValueOrDefault(seedB);

            var node = new BracketNode
            {
                BracketDetailId = bracket.Id,
                Side = BracketSide.Winners,
                RoundNumber = 1,
                PositionInRound = i,
                Slot1EntrantId = entrantA?.Id,
                Slot2EntrantId = entrantB?.Id,
                // A missing seed (the padded bracket is larger than the field) is a permanent bye,
                // not a pending feed.
                Slot1IsBye = entrantA is null,
                Slot2IsBye = entrantB is null
            };
            bracket.Nodes.Add(node);
            round1.Add(node);

            MaterializeRound1Match(tournament, node);
        }

        return round1;
    }

    private static void BuildWinnersRounds2AndUp(BracketDetail bracket, List<BracketNode> round1, BracketSide side = BracketSide.Winners)
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
                    Side = side,
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

    /// <summary>
    /// Advances a completed node's winner into its downstream node's correct slot, then resolves
    /// that node (see <see cref="AdvanceInto"/>). Returns every Match newly materialized as a result.
    /// </summary>
    private List<Match> PropagateWinner(Tournament tournament, BracketDetail bracket, BracketNode completedNode, Guid winnerEntrantId)
    {
        if (completedNode.FeedsIntoWinnerNodeId is null)
        {
            tournament.Status = TournamentStatus.Completed;
            return new List<Match>();
        }

        var targetNode = bracket.Nodes.First(n => n.Id == completedNode.FeedsIntoWinnerNodeId);
        SetSlot(targetNode, completedNode.FeedsIntoWinnerSlot ?? (completedNode.PositionInRound % 2 == 0 ? 1 : 2), winnerEntrantId, isBye: false);
        return AdvanceInto(tournament, bracket, targetNode);
    }

    /// <summary>
    /// Propagates a *bye* forward from a phantom node (one whose two slots both ended up byes):
    /// the winner slot on the downstream node is marked a bye rather than filled with a player,
    /// so that node knows this input will never arrive.
    /// </summary>
    private List<Match> PropagateWinnerBye(Tournament tournament, BracketDetail bracket, BracketNode phantomNode)
    {
        if (phantomNode.FeedsIntoWinnerNodeId is null)
        {
            return new List<Match>();
        }

        var targetNode = bracket.Nodes.First(n => n.Id == phantomNode.FeedsIntoWinnerNodeId);
        SetSlot(targetNode, phantomNode.FeedsIntoWinnerSlot ?? (phantomNode.PositionInRound % 2 == 0 ? 1 : 2), entrantId: null, isBye: true);
        return AdvanceInto(tournament, bracket, targetNode);
    }

    /// <summary>
    /// Drops a winners-bracket match's loser into its wired losers-bracket (or, for a 2-entrant
    /// bracket, Grand Final) node, then resolves that node. A losers-bracket loss is never
    /// propagated further - there is no FeedsIntoLoserNodeId chain beyond the winners bracket.
    /// </summary>
    private List<Match> PropagateLoser(Tournament tournament, BracketDetail bracket, BracketNode completedWbNode, Guid loserEntrantId)
    {
        var targetNode = bracket.Nodes.First(n => n.Id == completedWbNode.FeedsIntoLoserNodeId);
        SetSlot(targetNode, completedWbNode.FeedsIntoLoserSlot ?? 2, loserEntrantId, isBye: false);
        return AdvanceInto(tournament, bracket, targetNode);
    }

    /// <summary>
    /// Marks the losers-bracket slot that a winners-bracket bye would have fed as a bye - a bye
    /// produces no loser to drop. Cascades: two byes feeding one losers node make it a phantom.
    /// </summary>
    private List<Match> PropagateLoserBye(Tournament tournament, BracketDetail bracket, BracketNode completedWbNode)
    {
        if (completedWbNode.FeedsIntoLoserNodeId is null)
        {
            return new List<Match>();
        }

        var targetNode = bracket.Nodes.First(n => n.Id == completedWbNode.FeedsIntoLoserNodeId);
        SetSlot(targetNode, completedWbNode.FeedsIntoLoserSlot ?? 2, entrantId: null, isBye: true);
        return AdvanceInto(tournament, bracket, targetNode);
    }

    private static void SetSlot(BracketNode node, int slot, Guid? entrantId, bool isBye)
    {
        if (slot == 1)
        {
            node.Slot1EntrantId = entrantId;
            if (isBye) node.Slot1IsBye = true;
        }
        else
        {
            node.Slot2EntrantId = entrantId;
            if (isBye) node.Slot2IsBye = true;
        }
    }

    /// <summary>
    /// Resolves a node once one of its slots has just been set, and returns every Match newly
    /// materialized - cascading through byes:
    ///  - two entrants        -&gt; a Scheduled match;
    ///  - one entrant + a bye  -&gt; a Completed bye match, then that winner advances immediately;
    ///  - two byes             -&gt; no match; a bye propagates on to the next node.
    /// A node still missing a slot (neither an entrant nor a known bye) yields nothing yet - so a
    /// round-2+/losers node never auto-completes just because one feeder hasn't been played.
    /// </summary>
    private List<Match> AdvanceInto(Tournament tournament, BracketDetail bracket, BracketNode node)
    {
        if (node.MatchId is not null || !node.Slot1Resolved || !node.Slot2Resolved)
        {
            return new List<Match>();
        }

        var hasS1 = node.Slot1EntrantId is not null;
        var hasS2 = node.Slot2EntrantId is not null;

        if (hasS1 && hasS2)
        {
            var match = new Match
            {
                TournamentId = tournament.Id,
                BracketNodeId = node.Id,
                Player1EntrantId = node.Slot1EntrantId!.Value,
                Player2EntrantId = node.Slot2EntrantId!.Value,
                Status = MatchStatus.Scheduled
            };
            AttachMatch(tournament, node, match);
            return new List<Match> { match };
        }

        if (hasS1 || hasS2)
        {
            var winnerId = node.Slot1EntrantId ?? node.Slot2EntrantId!.Value;
            var byeMatch = new Match
            {
                TournamentId = tournament.Id,
                BracketNodeId = node.Id,
                Player1EntrantId = winnerId,
                Player2EntrantId = null,
                WinnerEntrantId = winnerId,
                Status = MatchStatus.Completed
            };
            AttachMatch(tournament, node, byeMatch);

            var produced = new List<Match> { byeMatch };
            produced.AddRange(PropagateWinner(tournament, bracket, node, winnerId));
            return produced;
        }

        // Both slots byes: a phantom node - no match, no player, but the bye travels onward.
        return PropagateWinnerBye(tournament, bracket, node);
    }

    private static void AttachMatch(Tournament tournament, BracketNode node, Match match)
    {
        tournament.Matches.Add(match);
        node.Match = match;
        node.MatchId = match.Id;
    }

    /// <summary>
    /// Round 1 has complete information immediately: a slot with no entrant is a permanent bye
    /// (that seed doesn't exist), not a pending feed. One real entrant auto-completes as a bye win;
    /// two entrants make a scheduled match.
    /// </summary>
    private void MaterializeRound1Match(Tournament tournament, BracketNode node)
    {
        var hasS1 = node.Slot1EntrantId is not null;
        var hasS2 = node.Slot2EntrantId is not null;
        if (!hasS1 && !hasS2)
        {
            return;
        }

        Match match;
        if (hasS1 && hasS2)
        {
            match = new Match
            {
                TournamentId = tournament.Id,
                BracketNodeId = node.Id,
                Player1EntrantId = node.Slot1EntrantId!.Value,
                Player2EntrantId = node.Slot2EntrantId!.Value,
                Status = MatchStatus.Scheduled
            };
        }
        else
        {
            var winnerId = node.Slot1EntrantId ?? node.Slot2EntrantId!.Value;
            match = new Match
            {
                TournamentId = tournament.Id,
                BracketNodeId = node.Id,
                Player1EntrantId = winnerId,
                Player2EntrantId = null,
                WinnerEntrantId = winnerId,
                Status = MatchStatus.Completed
            };
        }

        AttachMatch(tournament, node, match);
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
