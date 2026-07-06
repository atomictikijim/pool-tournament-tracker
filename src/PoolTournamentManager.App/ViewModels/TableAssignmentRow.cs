using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.App.ViewModels;

public record TableAssignmentRow(Table Table, MatchRowViewModel? CurrentMatch)
{
    public string StatusText => CurrentMatch is null
        ? "Open"
        : $"{CurrentMatch.Player1Name} vs {CurrentMatch.Player2Name}";
}
