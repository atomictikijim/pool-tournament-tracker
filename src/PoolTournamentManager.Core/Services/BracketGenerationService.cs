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
        var seedSlots = BuildSeedSlotOrder(bracketSize);

        var bracket = new BracketDetail
        {
            TournamentId = tournament.Id,
            IsDoubleElimination = false
        };
        tournament.Bracket = bracket;

        var roundNodes = new List<BracketNode>();
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
            roundNodes.Add(node);

            MaterializeRound1Match(tournament, node);
        }

        // Build subsequent rounds, wiring each pair of nodes to feed the next round's node.
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
    /// Records a completed match's score, determines the winner, and advances that winner
    /// into the next bracket node - creating that node's Match once both its slots are filled.
    /// Returns the newly-created next-round Match, if advancing produced one, so the caller's
    /// persistence layer can explicitly track it as a new entity (it was attached to an
    /// already-tracked Tournament graph, so change-tracking can't infer that on its own).
    /// </summary>
    public Match? RecordMatchResult(Tournament tournament, Match match, int player1Score, int player2Score)
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
        if (bracket is not null && node is not null)
        {
            return PropagateWinner(tournament, bracket, node, match.WinnerEntrantId.Value);
        }

        return null;
    }

    private Match? PropagateWinner(Tournament tournament, BracketDetail bracket, BracketNode completedNode, Guid winnerEntrantId)
    {
        if (completedNode.FeedsIntoWinnerNodeId is null)
        {
            tournament.Status = TournamentStatus.Completed;
            return null;
        }

        var targetNode = bracket.Nodes.First(n => n.Id == completedNode.FeedsIntoWinnerNodeId);
        if (completedNode.PositionInRound % 2 == 0)
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
    /// Round 2+ nodes must never auto-complete as a bye: an empty second slot here always means
    /// "the other semifinal hasn't been played yet", not "no opponent will ever arrive".
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
