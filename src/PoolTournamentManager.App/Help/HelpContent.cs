using System.Collections.Generic;

namespace PoolTournamentManager.App.Help;

/// <summary>Identifies which tab a help request came from, so <see cref="HelpContentProvider"/>
/// can return the matching contextual guide.</summary>
public enum HelpTopic
{
    Players,
    Teams,
    Tournament,
    TournamentSettings,
}

/// <summary>The kind of a single block of help text, used by HelpWindow's item template to style
/// it (heading vs. paragraph vs. bullet). Keeps the content itself free of any presentation markup.</summary>
public enum HelpBlockKind
{
    Heading,
    Subheading,
    Paragraph,
    Bullet,
    SubBullet,
}

/// <summary>One styled line/paragraph of a help document.</summary>
public sealed record HelpBlock(HelpBlockKind Kind, string Text);

/// <summary>A whole tab's contextual help: a window title plus the ordered blocks to render.</summary>
public sealed record HelpDocument(string Title, IReadOnlyList<HelpBlock> Blocks);

/// <summary>
/// Supplies the contextual help shown by the "?" button on each tab. The content mirrors the
/// end-user manual in FUNCTIONS.md at the solution root - if the app's user-visible behavior
/// changes, update both together (see CLAUDE.md's documentation policy).
/// </summary>
public static class HelpContentProvider
{
    public static HelpDocument For(HelpTopic topic) => topic switch
    {
        HelpTopic.Players => Players(),
        HelpTopic.Teams => Teams(),
        HelpTopic.Tournament => Tournament(),
        HelpTopic.TournamentSettings => TournamentSettings(),
        _ => new HelpDocument("Help", new List<HelpBlock>()),
    };

    private static HelpDocument Players() => new("Players Tab — Help", new Builder()
        .H("Players")
        .P("The Players tab holds your persistent roster of individual players. Anyone you add here is saved automatically and can be reused across any number of tournaments. The grid lists every player; the toolbar above it has New Player, Edit and Delete. Creating and editing both happen in a small pop-up window, so the grid always shows your saved roster.")
        .Sub("Adding a player")
        .B("Click New Player to open the editor window.")
        .B("Fill in First Name and Last Name (required). Email and Phone are optional.")
        .B("Optionally enter one or more ratings — Fargo, TAP, APA 8-Ball skill (1–9) and APA 9-Ball skill (1–9). These are only used if you later choose to seed a tournament by rating, so leave any you don't track blank.")
        .B("Click Save. If something required is missing or a rating is out of range, the window stays open and shows the problem in red. Cancel discards your changes.")
        .Sub("Editing a player")
        .P("Select a row and click Edit — or just double-click the row. The same window opens pre-filled; change any field and click Save.")
        .Sub("Deleting players")
        .P("Select one or more rows (hold Ctrl to pick several, or Shift to select a range) and click Delete. You'll always be asked to confirm first, since deletion can't be undone. A player currently entered in a tournament can't be deleted — the status line at the bottom names anyone skipped for that reason.")
        .Build());

    private static HelpDocument Teams() => new("Teams Tab — Help", new Builder()
        .H("Teams")
        .P("The Teams tab works exactly like the Players tab, with its own New Team, Edit and Delete toolbar and the same pop-up editor. A team is just a name plus two optional descriptive fields, and is used as an entrant in the formats that support team play.")
        .Sub("Adding a team")
        .B("Click New Team to open the editor.")
        .B("Enter the Team Name (required).")
        .B("Optionally fill in Division (a short code such as \"1\" or \"A\") and Location (the pool hall the team plays out of). Neither affects scheduling or seeding — they're informational, shown in the grid so you can tell similarly-named teams apart.")
        .B("Click Save (or Cancel to discard).")
        .Sub("Editing and deleting")
        .P("Edit (or double-click) a selected row to change it, and Delete selected rows after confirming — exactly as on the Players tab. A team that is entered in a tournament can't be deleted.")
        .Sub("Where teams are used")
        .P("Teams can only be entrants in Single, Double and Modified Single Elimination — tick Use Teams on the Tournament Settings tab. Round Robin, Ring Game and Chip Tournament are players-only.")
        .Build());

    private static HelpDocument TournamentSettings() => new("Tournament Settings Tab — Help", new Builder()
        .H("Tournament Settings")
        .P("This tab is where you create and configure a new tournament. Fill in the form on the left, choose entrants in the middle, then click Create Tournament — the app switches to the Tournament tab and opens your new event automatically.")
        .Sub("The form")
        .B("Name — whatever you want to call the event.")
        .B("Game — 8-Ball, 9-Ball or 10-Ball. This is a label only; it doesn't change any scoring or bracket logic.")
        .B("Format — Single Elimination, Double Elimination, Modified Single Elimination, Round Robin, Ring Game or Chip Tournament (see Formats, below).")
        .B("Use Teams — appears for the three elimination formats; tick it to enter Teams instead of individual Players.")
        .B("Seed by rating — appears for Single/Double Elimination and Round Robin when using Players; choose which rating to seed entrants by. Players with no rating on file are seeded last.")
        .B("Number of tables — required for every format except Ring Game. Matches can't be started until the tournament has this many tables (you can add more later from the Tournament tab).")
        .B("Format-specific fields appear automatically: Ring Game asks for the buy-in and 5-ball/9-ball payouts; Chip Tournament asks for starting chips per player.")
        .B("Entrants — tick everyone who's playing. Click Refresh if you just added someone on another tab. The Filter panel on the right narrows a long roster without ever unchecking anyone already selected.")
        .Sub("Entrant count requirements")
        .B("Double Elimination requires an exact power of 2 (2, 4, 8, 16, 32…).")
        .B("Modified Single Elimination needs entrants that divide into brackets of 6-8 (6-8 for one bracket, 12-16 for two, 18-24 for three, and so on). Counts that would leave a bracket under 6 - 9, 10, 11 and 17 - aren't allowed.")
        .B("Single Elimination, Round Robin, Ring Game and Chip Tournament accept any count of 2 or more.")
        .P("If you pick an unsupported count, the app tells you exactly what's required instead of creating a broken bracket.")
        .Sub("Entry fees and prize payouts")
        .P("Every format except Ring Game can charge an entry fee and pay out a prize pool by finishing place:")
        .B("Entry fee ($) — what each entrant pays. A live total shows the fee times the number of entrants currently checked.")
        .B("Tournament host fee (%) — the cut the organizer keeps before anything is paid out. Leave at 0 if the whole entry fee goes to prizes.")
        .B("Number of payout places — how many finishing places get paid (e.g. 3 for 1st/2nd/3rd). Leave at 0 for no prizes.")
        .B("Place N: % — each place's share of the prize pool (entry fees minus the host cut). The percentages across all places must add up to exactly 100% before the tournament can be created.")
        .Sub("Editing an existing tournament")
        .P("While a tournament is still Not Started you can reopen it here using the Edit Tournament button on the Tournament tab; every field pre-fills from its current settings and Create Tournament becomes Save Settings. Saving rebuilds the bracket from scratch but keeps it as the same tournament.")
        .Sub("The formats")
        .B("Single Elimination — classic knockout; lose once and you're out. Seeded so the strongest players meet as late as possible; non-power-of-2 fields give byes to the top seeds.")
        .B("Double Elimination — losing once drops you to a Losers bracket instead of eliminating you. The Winners- and Losers-bracket champions meet in a Grand Final, with a possible bracket-reset rematch if the Losers-bracket champion wins it.")
        .B("Modified Single Elimination — APA's qualifier format; everyone is guaranteed at least two matches. Entrants split into independent brackets of 6-8, and each bracket crowns its own winner (so a field of 24 produces three winners). Brackets never cross - entrants stay in the bracket they were drawn into. Winners are shown as \"Qualified\" in the Final Results; there is no prize pool for this format.")
        .B("Round Robin — no elimination; everyone plays everyone once. Standings rank by wins, then head-to-head, then point differential, then games-won %.")
        .B("Ring Game — a rotation 9-ball money game (not a bracket): players buy in, shoot in a fixed order, and cash out individually.")
        .B("Chip Tournament — a lives game: every player starts with the same chips, loses one per loss, and is eliminated at zero. The last player standing wins.")
        .Build());

    private static HelpDocument Tournament() => new("Tournament Tab — Help", new Builder()
        .H("Tournament")
        .P("This tab is where you run a tournament once it has been created. The left column lists every tournament you've made — click one to select and operate it on the right. (You create new tournaments on the Tournament Settings tab.) Use the Status drop-down above the list to show only Not Started, In Progress or Completed events.")
        .Sub("Status: Not Started, In Progress, Completed")
        .P("Elimination and Round Robin tournaments start out Not Started — the bracket already exists so you can review the matchups, but nothing is locked in. They become In Progress the instant you Start the first match, and Completed once the champion is decided. Ring Game and Chip Tournament skip straight to In Progress.")
        .Sub("Before play begins (Not Started only)")
        .B("Reshuffle Bracket re-draws the bracket from a completely random shuffle of the same entrants — it always ignores rating seeding. Reshuffle as often as you like until the first match starts.")
        .B("Edit Tournament reopens the tournament's settings on the Tournament Settings tab.")
        .B("An Add Player / Add Team picker lets you add an entrant, which regenerates the bracket. All three of these disappear once the first match starts.")
        .Sub("Deleting a tournament")
        .P("Delete Tournament permanently removes the selected tournament with its bracket, matches, tables and entrant list, after a confirmation. The players and teams themselves stay on their rosters for other tournaments.")
        .Sub("Tables")
        .P("The Tables count shows how many tables you've added. Add Table adds another on the fly. A table you pick for a match is saved automatically as soon as you Start that match — there's no separate save step.")
        .Sub("Playing a bracket or Round Robin")
        .B("In a match box, pick a table from the dropdown (only tables not already in use are listed) and click Start. You can't start a match without picking a table.")
        .B("While in progress the box shows a live mm:ss timer and a Finish button.")
        .B("Enter both scores, then click Finish. The winner is highlighted and advances automatically, and the box shows the final match duration.")
        .B("Round Robin shows round columns plus a live Standings panel instead of a bracket tree; Start/Finish and score entry work the same.")
        .B("For a large bracket, use the Zoom controls (or Ctrl+mouse-wheel over the bracket); Fit sizes the whole bracket to the screen.")
        .Sub("How Round Robin standings are decided")
        .P("Round Robin has no elimination — the winner is simply whoever tops the standings after everyone has played everyone once. The order is decided by applying these rules in sequence, each one used only to break a tie the rule above it couldn't separate:")
        .B("Match wins — most wins first.")
        .B("Head-to-head — among players tied on wins, whoever won the match(es) played between just those tied players ranks higher. Beating the person you're level with is treated as more meaningful than your scores against everyone else.")
        .B("Point differential — the \"Diff\" column: total games (racks) won minus total games lost across all your matches. It rewards winning by bigger margins, so a 5–1 record built on 7–2 wins outranks a 5–1 record built on 7–6 wins.")
        .B("Games-won % — games won out of all games played, as a final numeric tiebreak (then player name, purely so the order is always deterministic).")
        .P("Because the rules are applied strictly in that order, a player can finish ahead of someone who has a better Diff and a higher games-won %: if the two are tied on match wins and one beat the other head-to-head, that head-to-head result settles it and the later measures are never even looked at.")
        .Sub("Running a Ring Game")
        .B("Made the 5 — the current shooter pockets the 5-ball, is paid the 5-ball payout, and keeps shooting.")
        .B("Made the 9 — the current shooter pockets the 9-ball, is paid, the rack ends, and the break passes to the next player.")
        .B("Miss / Next Player — the turn passes to the next active player with no payout.")
        .B("Cash Out (on a player's card) — that player leaves the game and their net is locked in. The session ends when one player remains. A live Ledger tracks every player's buy-in, winnings and net.")
        .Sub("Running a Chip Tournament")
        .B("Click Shuffle & Seat Players once before the first game to randomly seat players two-per-table; extras wait in the Next Up queue. You can re-shuffle until the first game is recorded.")
        .B("When a player wins their table, click Wins next to their name. The loser drops one chip and — if they still have chips — goes to the back of the queue; at zero chips they're eliminated and their finishing place is locked in.")
        .B("The Next Up list shows who's waiting; the standings grid shows finishing place, chip count, games won and win %. The last player holding chips wins.")
        .Sub("Prize Payouts")
        .P("If payouts are configured, a Prize Payouts panel shows the entry-fee, host-cut and prize-pool totals plus each place's payout. Round Robin and Chip Tournament update live; elimination formats show payouts once complete, grouping ties (e.g. \"3rd–4th (tied)\") and splitting their combined payout evenly.")
        .Sub("Final Results")
        .P("Once a tournament completes, a Final Results column appears on the right (and on the Display window), listing every entrant in finishing order along with any prize their place earned. It shows for all placement formats even when no prizes are configured — the prize column is simply blank then. Tied bracket places are shown as a range (e.g. \"3rd-4th\"). For Round Robin and Chip Tournament this order is exact; for elimination brackets the champion and runner-up are exact and lower places are ordered by win/loss record. It replaces the Prize Payouts panel at completion so the same information isn't shown twice.")
        .Sub("Display window")
        .P("Open Display Window opens a read-only, projector-friendly second window that mirrors the live bracket/standings/board, prize payouts and final results in real time, over a faded ball graphic matching the game. Everything on it is purely for the audience to watch — all control stays on this tab.")
        .B("Full Screen (or press F11) hides the window chrome and fills the whole screen for projecting to the room; press Esc or the button again to return to a normal window.")
        .Build());

    /// <summary>Tiny fluent helper so each document above reads as an ordered outline rather than a
    /// wall of <c>new HelpBlock(...)</c> calls.</summary>
    private sealed class Builder
    {
        private readonly List<HelpBlock> _blocks = new();

        public Builder H(string text) => Add(HelpBlockKind.Heading, text);
        public Builder Sub(string text) => Add(HelpBlockKind.Subheading, text);
        public Builder P(string text) => Add(HelpBlockKind.Paragraph, text);
        public Builder B(string text) => Add(HelpBlockKind.Bullet, text);
        public Builder SB(string text) => Add(HelpBlockKind.SubBullet, text);

        private Builder Add(HelpBlockKind kind, string text)
        {
            _blocks.Add(new HelpBlock(kind, text));
            return this;
        }

        public IReadOnlyList<HelpBlock> Build() => _blocks;
    }
}
