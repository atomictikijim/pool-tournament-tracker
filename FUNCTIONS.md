# Pool Tournament Manager — User Guide

This is a step-by-step guide to running a tournament with Pool Tournament Manager,
covering every tab, button, and format the app supports.

## Contents

1. [Getting started](#1-getting-started)
2. [Managing players (Players tab)](#2-managing-players-players-tab)
3. [Managing teams (Teams tab)](#3-managing-teams-teams-tab)
4. [Creating a tournament (Tournament Settings tab)](#4-creating-a-tournament-tournament-settings-tab)
5. [Running a tournament (Tournament tab)](#5-running-a-tournament-tournament-tab)
6. [Entry fees and prize payouts](#6-entry-fees-and-prize-payouts)
7. [The tournament formats explained](#7-the-tournament-formats-explained)
8. [The Display window](#8-the-display-window)
9. [Appearance](#9-appearance)

---

## 1. Getting started

Launch the app (double-click the exe, or run it from the project as described in
[README.md](README.md)). It opens to the **Players** tab. All of your data — players,
teams, and tournaments — is saved automatically to a local file on your computer;
there is nothing to save manually and nothing to configure before you start.

The app has four tabs across the top:

- **Players** — your roster of individual players.
- **Teams** — your roster of teams (for team-based tournaments).
- **Tournament** — where you run a tournament once it's created: assign tables,
  start/finish matches, watch the bracket or standings.
- **Tournament Settings** — where you create a new tournament and configure it.

## 2. Managing players (Players tab)

The left-hand grid lists every player you've added. Click a row to load that
player into the **Player Details** panel on the right for editing.

To add a new player:

1. Click **New Player**. This clears the details panel.
2. Fill in **First Name** and **Last Name** (required). Email and Phone are optional.
3. Optionally fill in one or more ratings — **Fargo Rating**, **TAP Rating**,
   **APA 8-Ball Skill** (1–9), **APA 9-Ball Skill** (1–9). These are only used if you
   choose to seed a tournament by rating (see [Section 4](#4-creating-a-tournament-tournament-settings-tab)) — leave any you don't
   track blank.
4. Click **Save**.

To edit an existing player, click their row, change any field, and click **Save**
again.

There's no delete option — a player who shouldn't be entered into a tournament
simply isn't selected as an entrant.

## 3. Managing teams (Teams tab)

Works the same way as Players, but simpler — a team is just a name (no player
roster or membership tracking).

1. Click **New Team**.
2. Enter the **Team Name**.
3. Click **Save**.

Teams are only usable as entrants in formats that support them (see below).

## 4. Creating a tournament (Tournament Settings tab)

This tab holds the entire "create a new tournament" form.

1. **Name** — whatever you want to call the event.
2. **Game** — 8-Ball, 9-Ball, or 10-Ball. This is a label only; it doesn't change any
   scoring or bracket logic.
3. **Format** — pick one of the five formats (see [Section 7](#7-the-tournament-formats-explained) for full rules on
   each):
   - Single Elimination
   - Double Elimination
   - Modified Single Elimination
   - Round Robin
   - Ring Game
   - Chip Tournament
4. **Use Teams** checkbox — appears only for Single Elimination, Double Elimination,
   and Modified Single Elimination. Check it to run the event with Teams as entrants
   instead of individual Players. (Round Robin, Ring Game, and Chip Tournament are
   Players-only.)
5. **Seed by rating** — appears for Single/Double Elimination and Round Robin when
   using Players (not shown for Modified Single Elimination, which always seeds
   randomly, and not shown when Use Teams is checked). Choose which rating
   (Fargo/TAP/APA 8-Ball/APA 9-Ball) to seed entrants by. Players with no rating on
   file are seeded last. If you don't need seeding to matter, any choice is fine —
   entrants with no ratings at all will simply seed in name order.
6. **Number of tables** — required for every format except Ring Game. This is how
   many physical tables you have available; matches can't be started until the
   tournament has this many tables (you can also add more later from the
   Tournament tab).
7. Format-specific setup fields appear automatically:
   - **Ring Game**: Buy-in ($), 5-ball payout ($), 9-ball payout ($).
   - **Chip Tournament**: Starting chips per player.
8. **Entry fee and prize payouts** — appears for every format except Ring Game.
   See [Section 6](#6-entry-fees-and-prize-payouts) for the full explanation; this is
   where you set the entry fee, the tournament host's cut, and how the remaining
   prize pool is split across finishing places.
9. **Entrants** — a checklist of your Players (or Teams, if Use Teams is checked)
   to include. Check everyone who's playing. Click **Refresh** if you added a
   player/team on another tab and don't see them yet.
10. Click **Create Tournament**.

Entrant count requirements:

- **Double Elimination** requires an exact power-of-2 entrant count (2, 4, 8, 16,
  32...).
- **Modified Single Elimination** requires a multiple of 8 that's also a power of 2
  (8, 16, 32, 64...).
- Single Elimination, Round Robin, Ring Game, and Chip Tournament accept any entrant
  count (2 or more).

If you pick an unsupported count, the app tells you exactly what's required instead
of creating a broken bracket.

Once created, switch to the **Tournament** tab — your new tournament appears in the
list on the left.

## 5. Running a tournament (Tournament tab)

The left column lists every tournament you've created — click one to select it and
work with it on the right.

### Tables

The **Tables** row along the top shows every table you've added. Click **Add Table**
to add another one on the fly (useful if a table frees up from another event, or you
under-counted at creation). **Save Table Assignments** persists any table changes
you've made in the bracket/round view below.

### Adding entrants after creation

If the tournament hasn't had any match/game actually start yet, an **Add Player:**
or **Add Team:** picker appears (whichever the tournament uses) — pick someone from
the dropdown and click **Add**. For Single/Double/Modified Single Elimination and
Round Robin, adding an entrant regenerates the whole bracket/schedule from scratch,
so do this only before play begins. Once a match has started, this row disappears —
entrants are locked in for the rest of the event.

### Playing an elimination bracket (Single/Double/Modified Single Elimination)

The bracket displays as a tree, rounds running left to right. Each match box shows
both entrants (with their seed number, if seeded) and a score field for each.

To play a match:

1. In the match's box, pick a **table** from the dropdown and click **Start**.
   - You can't start a match without picking a table.
   - You can't start a match on a table that already has another match in progress
     on it.
2. While in progress, the box shows a live running timer (mm:ss) and a **Finish**
   button.
3. Enter both players'/teams' scores in the score boxes, then click **Finish**.
   - The winner is highlighted (bold, accent color) in the bracket immediately, and
     they automatically advance to their next match.
   - The box now shows "Finished in mm:ss" underneath — the match's final duration.
4. Repeat for every match until a champion is decided. The tournament's status
   updates automatically once the final is complete.

Double Elimination: a loss doesn't eliminate you the first time — you drop to the
Losers side and keep playing until you lose there too. If the Losers-side champion
beats the Winners-side champion in the Grand Final, there's one more rematch (a
"bracket reset") to decide the tournament, since the Winners-side champion hasn't
lost yet.

Modified Single Elimination: see [Section 7](#7-the-tournament-formats-explained) for the exact structure — every
entrant gets at least two matches, but it moves faster than full Double Elimination.

If the tournament has prize payouts configured, a **Prize Payouts** panel appears
below the bracket once the champion and runner-up are decided (see
[Section 6](#6-entry-fees-and-prize-payouts)).

### Playing a Round Robin

Instead of a bracket tree, you get a set of round columns — every entrant plays
every other entrant exactly once across the tournament. Start/Finish/timer and
score entry work exactly the same as elimination matches. A live **Standings** panel
above the bracket area shows rank, wins, losses, point differential, and games-won
%, updating after every reported result. The champion (the #1 standings row) is
announced automatically once every scheduled match is complete.

### Running a Ring Game

Ring Game has no bracket — it shows a status line ("Rack N · Pot $X · Up: [player]")
and a row of player cards in rotation order, with the current shooter's card
outlined and any cashed-out player's card dimmed.

- **Made the 5** — the current shooter pocketed the 5-ball: they're paid the 5-ball
  payout, and play continues (they stay at the table).
- **Made the 9** — the current shooter pocketed the 9-ball: they're paid the 9-ball
  payout, the rack ends, and the break passes to the next player in rotation.
- **Miss / Next Player** — the shooter's turn ends without a payout; the turn passes
  to the next active player.
- **Cash Out** (on a player's card) — that player leaves the game and their final net
  win/loss is locked in. The session ends automatically once only one player
  remains.

A **Ledger** grid below lists every player's buy-in, total winnings, and net
result — the money always balances (everyone's net sums to the pot still on the
table).

### Running a Chip Tournament

Chip Tournament also has no bracket. A status line shows how many players remain,
how many chips everyone started with, and the pot. Below that:

1. Pick the **Winner** and the player they **beat** from the two dropdowns (both
   lists only show players still in the tournament).
2. Click **Record Game**.
3. The loser drops one chip; the winner's chip count doesn't change. A player who
   hits zero chips is eliminated and locks in a finishing place.

The **standings grid** updates live after every game: finishing place, player, and
current chip count. If prize payouts are configured, a separate **Prize Payouts**
panel below shows what each finishing place wins (see
[Section 6](#6-entry-fees-and-prize-payouts)). The tournament completes automatically once one player is left holding
chips — they're the champion.

## 6. Entry fees and prize payouts

Every format except Ring Game (which has its own separate buy-in and
5-ball/9-ball payouts — see below) can charge an entry fee and pay out a prize
pool by finishing place. This is all configured on the Tournament Settings
tab when you create the tournament:

- **Entry fee ($)** — how much each entrant pays. Next to it, a live **Total
  entry fees collected** readout shows the entry fee times however many
  entrants you currently have checked in the Entrants list below — it updates
  as you check/uncheck entrants.
- **Tournament host fee (%)** — the percentage of that total the tournament
  organizer/host keeps before anything is paid out. Leave at 0 if the entire
  entry fee goes to prizes.
- **Number of payout places** — how many finishing places get paid (e.g. 3 for
  1st/2nd/3rd). Leave at 0 for no configured payouts at all (you can still
  charge an entry fee purely to fund a house cut, with no prizes).
- One **Place N: __%** row per payout place — what percentage of the *prize
  pool* (the total entry fees minus the host's cut) that place receives. A
  live "Total: XX%" hint shows the running sum; the percentages across all
  configured places must add up to exactly 100% before the tournament can be
  created.

Example: 8 entrants pay a $10 entry fee ($80 total). A 10% host fee takes $8,
leaving a $72 prize pool. If 1st/2nd/3rd are set to 60%/30%/10%, 1st place
wins $43.20, 2nd wins $21.60, and 3rd wins $7.20.

Once the tournament is running, a **Prize Payouts** panel (on the Tournament
tab, and mirrored on the [Display window](#8-the-display-window)) shows the
entry-fee/host-cut/prize-pool totals and, once finishing places are known,
each place's payout:

- **Round Robin and Chip Tournament** show live, updating payouts throughout
  the event, since every entrant's finishing place is always known (Round
  Robin from the Standings ranking, Chip Tournament from elimination order).
- **Single/Double/Modified Single Elimination** only show payouts once the
  tournament is complete. The champion (1st) and runner-up (2nd) are always
  exact. For 3rd place and below, a bracket doesn't actually decide a strict
  order among everyone eliminated earlier — for example, both players who
  lose in the semifinals are simply "out in the semifinals," with nothing
  ranking one above the other. In that situation, the app groups tied
  entrants together (shown as e.g. "3rd-4th (tied)") and splits their
  combined payout evenly between them.

## 7. The tournament formats explained

### Single Elimination

Classic knockout bracket. Lose once, you're out. Entrants are seeded (by rating, or
randomly if no ratings are set) so that the strongest players/teams meet as late as
possible; if the entrant count isn't a power of 2, top seeds receive byes in the
first round.

### Double Elimination

Same seeded bracket, but losing once drops you to a **Losers bracket** instead of
eliminating you. You're only out once you lose there too. The Winners-bracket
champion and the Losers-bracket champion meet in a **Grand Final**; if the
Losers-bracket champion wins that match, there's one final rematch (since the
Winners-bracket champion hasn't lost a match yet) to decide the tournament.
Requires an exact power-of-2 entrant count.

### Modified Single Elimination

APA's format — a shorter, faster alternative to Double Elimination that still
guarantees everyone at least two matches. Entrants are split into groups of 8 called
**pods** (a 16-entrant tournament has 2 pods, a 32-entrant tournament has 4, etc.),
and each pod plays out independently:

1. **Round 1 (Winners)** — all 8 entrants in the pod, randomly drawn (not
   rating-seeded), play 4 matches.
2. **Losers Round 1** — the 4 Round-1 losers play each other. **Lose here and you're
   eliminated** — you've had your two guaranteed matches.
3. **Winners Round 2** — the 4 Round-1 winners (still unbeaten) play each other.
4. **Losers Round 2** — the losers of Winners Round 2 drop down to play the winners
   of Losers Round 1. Lose this match and you're eliminated.
5. **Final Four** — the 2 unbeaten Winners-Round-2 winners each play one of the 2
   Losers-Round-2 winners. The 2 winners of these matches become that pod's
   representatives.

Every pod's 2 representatives then feed into one ordinary single-elimination
bracket (a straight semifinal/final for a 2-pod, 16-entrant tournament; quarterfinal
onward for larger fields) — **from this point on it's plain single elimination**,
with no more consolation matches and no bracket reset. Requires an entrant count
that's a multiple of 8 and also a power of 2 (8, 16, 32, 64...).

### Round Robin

No elimination at all — every entrant plays every other entrant exactly once.
Standings are ranked by:

1. Wins (most wins first).
2. Among entrants tied on wins: head-to-head record against just the others they're
   tied with.
3. Still tied: point differential (points won minus points lost, across all
   matches).
4. Still tied: games-won percentage.

### Ring Game

A money game, not a bracket — based on rotation 9-ball rules. Every player buys in
(funding a shared pot) and is drawn into a fixed shooting order. The current shooter
keeps shooting until they miss; pocketing the 5-ball pays out and keeps them at the
table, pocketing the 9-ball pays out, ends the rack, and passes the break to the next
player. Any player can cash out at any point, locking in their net result; the
session ends when only one player is left.

### Chip Tournament

A "lives" tournament, not a bracket. Every player buys in and starts with the same
number of chips. Games are logged one at a time between any two still-active
players — the loser drops a chip, the winner is unaffected. A player at zero chips
is eliminated (their finishing place is locked in at that moment). The last player
still holding chips wins the tournament; see
[Section 6](#6-entry-fees-and-prize-payouts) for how payouts are configured.

## 8. The Display window

Click **Open Display Window** (on the Tournament tab) to open a second, read-only
window meant for a projector or a second monitor facing the room. It automatically
mirrors whatever's happening on the Tournament tab in real time — no manual refresh
needed:

- Tournament name and status.
- A "Now Playing" board showing which match is on which table.
- The live bracket (for elimination formats), Standings (Round Robin), rotation and
  money board (Ring Game), or standings board (Chip Tournament) — completed matches'
  winners are highlighted.
- The **Prize Payouts** panel, if the tournament has payouts configured (see
  [Section 6](#6-entry-fees-and-prize-payouts)) — not shown for Ring Game.

Nothing in the Display window is clickable/editable — it's purely for the audience
to watch. All control stays on the main Tournament tab.

## 9. Appearance

The app automatically matches your Windows light/dark mode setting and updates
instantly if you change it in Windows — there is nothing to configure in the app
itself.
