using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>The (table, winner, loser) triple submitted when the operator clicks a "Wins" button
/// on a chip-tournament table card - the single parameter type for RecordChipTableGameCommand.</summary>
public record ChipGameOutcome(Guid TableId, Guid WinnerId, Guid LoserId);

/// <summary>One table's card in the chip-tournament table board: who's seated in each seat, and
/// (when both seats are filled) the outcome to submit if that seat's player wins. Rebuilt from a
/// ChipTableSeat on every state change.</summary>
public class ChipTableBoardRowViewModel
{
    public Guid TableId { get; }
    public string TableLabel { get; }
    public string Player1Name { get; }
    public string Player2Name { get; }
    public bool IsReady { get; }
    public ChipGameOutcome? Player1WinsOutcome { get; }
    public ChipGameOutcome? Player2WinsOutcome { get; }

    public ChipTableBoardRowViewModel(ChipTableSeat seat)
    {
        TableId = seat.Table.Id;
        TableLabel = seat.Table.Label;
        Player1Name = seat.Player1?.Player?.FullName ?? "— waiting —";
        Player2Name = seat.Player2?.Player?.FullName ?? "— waiting —";
        IsReady = seat.Player1 is not null && seat.Player2 is not null;

        if (IsReady)
        {
            Player1WinsOutcome = new ChipGameOutcome(TableId, seat.Player1!.Id, seat.Player2!.Id);
            Player2WinsOutcome = new ChipGameOutcome(TableId, seat.Player2!.Id, seat.Player1!.Id);
        }
    }
}

/// <summary>One entrant's position in the chip tournament's "Next Up" rotation queue.</summary>
public class ChipNextUpRowViewModel
{
    public int Position { get; }
    public string PlayerName { get; }

    public ChipNextUpRowViewModel(int position, Core.Entities.TournamentEntrant entrant)
    {
        Position = position;
        PlayerName = entrant.Player?.FullName ?? "Unknown";
    }
}
