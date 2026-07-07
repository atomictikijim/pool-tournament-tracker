namespace PoolTournamentManager.Core.Enums;

// Values are explicit because they're persisted: appending InProgress here without pinning
// Scheduled/Completed would shift the stored ints and silently reinterpret existing rows.
public enum MatchStatus
{
    Scheduled = 0,
    Completed = 1,
    InProgress = 2
}
