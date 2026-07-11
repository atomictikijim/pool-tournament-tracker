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

        tournament.Status = TournamentStatus.NotStarted;
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

        ResolveFirstRoundByes(tournament, bracket);

        tournament.Status = TournamentStatus.NotStarted;
        return bracket;
    }

    /// <summary>
    /// Resolves every first-round bye now that the whole graph (and every FeedsInto* link) exists:
    /// advances each bye winner onward, and - for a winners-bracket bye that feeds a losers bracket -
    /// drops a "bye" into the losers slot where that match's (non-existent) loser would have gone.
    /// AdvanceInto cascades this, so two byes meeting collapse to a phantom that passes the bye on.
    /// Shared by double elimination and the modified-single-elimination pods.
    /// </summary>
    private void ResolveFirstRoundByes(Tournament tournament, BracketDetail bracket)
    {
        foreach (var byeNode in bracket.Nodes
                     .Where(n => n.Side == BracketSide.Winners && n.RoundNumber == 1 && n.Match is { IsBye: true })
                     .ToList())
        {
            PropagateWinner(tournament, bracket, byeNode, byeNode.Match!.WinnerEntrantId!.Value);
            if (byeNode.FeedsIntoLoserNodeId is not null)
            {
                PropagateLoserBye(tournament, bracket, byeNode);
            }
        }
    }

    private const int PodSize = 8;

    /// <summary>The fewest entrants a single bracket ("pod") may hold - a bracket smaller than this
    /// isn't run; the remaining slots up to <see cref="PodSize"/> become first-round byes.</summary>
    private const int MinPodSize = 6;

    /// <summary>
    /// True for entrant counts this format supports. Every group of 8 is a self-contained bracket
    /// that crowns its own winner, so the field is split into <c>ceil(count / 8)</c> independent
    /// pods (see <see cref="ModifiedSingleEliminationPodSizes"/>). Each pod must hold between
    /// <see cref="MinPodSize"/> (6) and <see cref="PodSize"/> (8) entrants, filling any empty slots
    /// with first-round byes. So a lone bracket needs 6-8; anything larger needs enough entrants to
    /// keep every pod at 6+ (e.g. 9-11 are invalid - a second bracket couldn't reach 6 - but 12+ is
    /// fine, as are 18+, 24+, etc.; 17 and 25 are invalid for the same "under 6 in a pod" reason).
    /// </summary>
    public static bool IsValidModifiedSingleEliminationCount(int entrantCount)
    {
        if (entrantCount < MinPodSize)
        {
            return false;
        }

        var podCount = (entrantCount + PodSize - 1) / PodSize; // ceil
        return entrantCount >= MinPodSize * podCount; // every pod can reach the 6-entrant floor
    }

    /// <summary>Sizes for <paramref name="entrantCount"/> split across the fewest pods that keep
    /// every pod between <see cref="MinPodSize"/> (6) and <see cref="PodSize"/> (8), as evenly as
    /// possible - e.g. 20 -&gt; [7, 7, 6], 12 -&gt; [6, 6], 24 -&gt; [8, 8, 8]. The larger pods come
    /// first. Only meaningful for counts <see cref="IsValidModifiedSingleEliminationCount"/> accepts.</summary>
    public static int[] ModifiedSingleEliminationPodSizes(int entrantCount)
    {
        var podCount = (entrantCount + PodSize - 1) / PodSize; // ceil
        var baseSize = entrantCount / podCount;
        var remainder = entrantCount % podCount;
        var sizes = new int[podCount];
        for (var i = 0; i < podCount; i++)
        {
            sizes[i] = baseSize + (i < remainder ? 1 : 0);
        }
        return sizes;
    }

    /// <summary>
    /// Builds an APA-style Modified Single Elimination bracket. Entrants are split as evenly as
    /// possible into pods of 6-8 (see <see cref="ModifiedSingleEliminationPodSizes"/>), and every
    /// pod is a fully self-contained bracket that crowns its own single winner - pods never cross,
    /// so a field of 24 (three pods) produces three co-equal winners (each "qualifies", e.g. for a
    /// higher-level event). Within a pod, round-1 losers get exactly one consolation match (a second
    /// loss there eliminates them) and round-2 losers get one more chance against those consolation
    /// survivors, converging on a Final Four and then a Bracket Final that decides the pod winner. A
    /// pod smaller than 8 fills its empty slots with first-round byes, placed by the standard seed
    /// chart so they spread across the pod. Round 1 is a random draw (not a rating seed) -
    /// SeedNumber records the draw order, so the byes land on random entrants and reshuffle with them.
    /// </summary>
    public BracketDetail GenerateModifiedSingleElimination(Tournament tournament)
    {
        var entrantCount = tournament.Entrants.Count;
        if (!IsValidModifiedSingleEliminationCount(entrantCount))
        {
            throw new InvalidOperationException(
                "Modified Single Elimination requires at least 6 entrants, split into independent brackets of 6-8.");
        }

        var drawn = SeedingService.RandomDraw(tournament.Entrants);

        var bracket = new BracketDetail
        {
            TournamentId = tournament.Id,
            Kind = BracketKind.ModifiedSingleElimination
        };
        tournament.Bracket = bracket;

        var podSizes = ModifiedSingleEliminationPodSizes(entrantCount);
        var drawIndex = 0;
        for (var p = 0; p < podSizes.Length; p++)
        {
            var podEntrants = drawn.GetRange(drawIndex, podSizes[p]);
            drawIndex += podSizes[p];
            BuildModifiedEliminationPod(tournament, bracket, podEntrants, p);
        }

        // Now that every pod's ladder is wired, resolve all first-round byes (in every pod) - the
        // cascade carries a pod's byes up through its own ladder to its Bracket Final.
        ResolveFirstRoundByes(tournament, bracket);

        tournament.Status = TournamentStatus.NotStarted;
        return bracket;
    }

    /// <summary>
    /// Builds one pod as an independent bracket: Round 1 -> Losers Round 1 (eliminates) -> Winners
    /// Round 2 -> Losers Round 2 (receiving) -> Final Four -> Bracket Final (the pod's single
    /// winner). The pod always has an 8-slot ladder; a pod with fewer than 8 entrants fills the
    /// extra slots with first-round byes, placed by the standard seed chart so they spread across
    /// the pod (and resolved later by <see cref="ResolveFirstRoundByes"/>). PositionInRound is
    /// offset by podIndex so every pod's same-named round shares one rendered column (and its
    /// Bracket Final sits at column position podIndex) instead of colliding at position 0/1.
    /// </summary>
    private void BuildModifiedEliminationPod(
        Tournament tournament, BracketDetail bracket, List<TournamentEntrant> podEntrants, int podIndex)
    {
        const int lanesPerPod = PodSize / 4; // 2 matches per pod at every post-round-1 stage

        // A full pod keeps the original draw-order pairing (draw seeds 1v2, 3v4, 5v6, 7v8). A
        // partial pod places its entrants by the standard 8-seed chart instead, so its byes spread
        // one-per-match across round 1 (every pod is at least 4 entrants, so no match is all byes)
        // rather than clustering into an empty "phantom" match at the end.
        var full = podEntrants.Count == PodSize;
        var seedSlots = BuildSeedSlotOrder(PodSize);
        var round1 = new List<BracketNode>();
        for (var i = 0; i < PodSize / 2; i++)
        {
            TournamentEntrant? entrantA;
            TournamentEntrant? entrantB;
            if (full)
            {
                entrantA = podEntrants[2 * i];
                entrantB = podEntrants[2 * i + 1];
            }
            else
            {
                var seedA = seedSlots[2 * i];
                var seedB = seedSlots[2 * i + 1];
                entrantA = seedA <= podEntrants.Count ? podEntrants[seedA - 1] : null;
                entrantB = seedB <= podEntrants.Count ? podEntrants[seedB - 1] : null;
            }

            var node = new BracketNode
            {
                BracketDetailId = bracket.Id,
                Side = BracketSide.Winners,
                RoundNumber = 1,
                PositionInRound = podIndex * (PodSize / 2) + i,
                Slot1EntrantId = entrantA?.Id,
                Slot2EntrantId = entrantB?.Id,
                Slot1IsBye = entrantA is null,
                Slot2IsBye = entrantB is null
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

        // Bracket Final: the pod's two Final-Four winners meet to crown this bracket's single
        // winner. It has no FeedsIntoWinnerNodeId, so it is a terminal "champion" node - every pod
        // has exactly one, and the tournament completes only once all of them are decided (see
        // AllChampionNodesComplete). Position podIndex keeps each pod's final in its own row.
        var bracketFinal = new BracketNode
        {
            BracketDetailId = bracket.Id,
            Side = BracketSide.Final,
            RoundNumber = 2,
            PositionInRound = podIndex
        };
        bracket.Nodes.Add(bracketFinal);

        finalFour[0].FeedsIntoWinnerNodeId = bracketFinal.Id;
        finalFour[0].FeedsIntoWinnerSlot = 1;
        finalFour[1].FeedsIntoWinnerNodeId = bracketFinal.Id;
        finalFour[1].FeedsIntoWinnerSlot = 2;
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
        // Continue numbering from the input round (single elimination passes round 1; the modified
        // single-elimination reps stage passes a later Final-side round), so built rounds don't
        // collide with the input round's number.
        var roundNumber = round1.Count > 0 ? round1[0].RoundNumber : 1;
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
            // This node is a bracket champion. Single/Double Elimination have exactly one, but
            // Modified Single Elimination has one per independent pod - so the tournament is only
            // complete once every champion node's match is decided, not the first one to finish.
            if (AllChampionNodesComplete(bracket))
            {
                tournament.Status = TournamentStatus.Completed;
            }
            return new List<Match>();
        }

        var targetNode = bracket.Nodes.First(n => n.Id == completedNode.FeedsIntoWinnerNodeId);
        SetSlot(targetNode, completedNode.FeedsIntoWinnerSlot ?? (completedNode.PositionInRound % 2 == 0 ? 1 : 2), winnerEntrantId, isBye: false);
        return AdvanceInto(tournament, bracket, targetNode);
    }

    /// <summary>
    /// True once every "champion" node - one with no onward winner feed - has a completed match.
    /// There is exactly one for Single Elimination (the final); Modified Single Elimination has one
    /// per independent pod (each pod's Bracket Final). Double Elimination decides completion in its
    /// own Grand Final branch and never reaches here. Champion nodes always have a real match (no
    /// pod is small enough to bye a whole side, and a single-elimination final always has players),
    /// so a null Match means "not decided yet" rather than a phantom.
    /// </summary>
    private static bool AllChampionNodesComplete(BracketDetail bracket) =>
        bracket.Nodes
            .Where(n => n.FeedsIntoWinnerNodeId is null && n.Side != BracketSide.Losers)
            .All(n => n.Match is { Status: MatchStatus.Completed });

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
