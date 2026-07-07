namespace PoolTournamentManager.Core.Enums;

// Values are explicit and match the old IsDoubleElimination bool's int representation
// (false = 0, true = 1) so the migration converting that column preserves existing data.
public enum BracketKind
{
    SingleElimination = 0,
    DoubleElimination = 1,
    ModifiedSingleElimination = 2
}
