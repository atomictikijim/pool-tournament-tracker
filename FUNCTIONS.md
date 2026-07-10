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

The grid lists every player you've added. Above it is a toolbar with **New
Player**, **Edit**, and **Delete**. Creating and editing both happen in a small
pop-up (modal) window, so the grid always shows your saved roster.

To add a new player:

1. Click **New Player**. A **New Player** window opens.
2. Fill in **First Name** and **Last Name** (required). Email and Phone are optional.
3. Optionally fill in one or more ratings — **Fargo Rating**, **TAP Rating**,
   **APA 8-Ball Skill** (1–9), **APA 9-Ball Skill** (1–9). These are only used if you
   choose to seed a tournament by rating (see [Section 4](#4-creating-a-tournament-tournament-settings-tab)) — leave any you don't
   track blank.
4. Click **Save**. If anything required is missing (or a rating is out of range),
   the window stays open and shows the problem in red so you can fix it. Click
   **Cancel** (or the window's close button) to discard the changes instead.

To edit an existing player, select their row and click **Edit** — or just
double-click the row. The same window opens pre-filled; change any field and
**Save**.

To delete players, select one or more rows (hold **Ctrl** to pick several, or
**Shift** to select a range) and click **Delete**. You'll always be asked to
confirm first, since deletion can't be undone. A player who is currently entered
in a tournament can't be deleted — the status line at the bottom names anyone
that was skipped for that reason.

## 3. Managing teams (Teams tab)

Works exactly like Players, with its own **New Team**, **Edit**, and **Delete**
toolbar and the same pop-up editor — a team is just a name plus two optional
descriptive fields.

1. Click **New Team**. A **New Team** window opens.
2. Enter the **Team Name** (required).
3. Optionally fill in **Division** (a short number or alphanumeric code, e.g. "1"
   or "A") and **Location** (the name of the pool hall the team plays out of).
   Neither is used in scheduling or seeding — they're informational, shown in
   the Teams grid for your own reference.
4. Click **Save** (or **Cancel** to discard).

Edit and delete work the same as on the Players tab: **Edit** (or double-click) a
selected row to change it, and **Delete** selected rows after confirming. A team
that is entered in a tournament can't be deleted.

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
   player/team on another tab and don't see them yet. The panel stretches to fill
   the window, resizing as you resize the app; the list itself scrolls once it has
   more entrants than fit. While **Seed by rating** is showing a system, each
   Player's checklist entry shows their rating for that system (e.g. "Alice
   Anderson (Fargo: 700)"), or a dash if they don't have one on file - this is the
   same rating that will later appear next to them in the bracket. When **Use
   Teams** is checked, each Team's entry likewise shows its Division and/or
   Location alongside the name when either is set (e.g. "Sharks (Div A · Corner
   Pocket)"), so you can tell similarly-named teams apart at a glance.
10. **Filter panel**, to the right of the Entrants checklist - narrows down a long
    roster without changing who's checked (filtering only hides rows, it never
    unchecks anyone already selected):
    - **Individual Players**: search by name, plus a min/max range on whichever
      rating "Seed by rating" currently has selected (players with no rating in
      that system are hidden while a range is set).
    - **Teams**: search by name, plus **Division** and **Location** drop-downs
      populated from whatever values are actually on your Team roster. Pick
      "(All)" on either to stop filtering by it.
11. Click **Create Tournament**.

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

### Filtering the list by status

Above the list is a **Status** drop-down. Leave it on **All** to see every
tournament, or pick **In Progress** or **Completed** to show only tournaments in
that state. This just hides rows from view — it doesn't change or delete anything,
and any selection you had is untouched when you change the filter.

### Deleting a tournament

Select a tournament in the left-hand list and click **Delete Tournament** (below the
list) to remove it permanently. You'll be asked to confirm first — deletion can't be
undone. This deletes the tournament along with its bracket, matches, tables, and
entrant list, but it does **not** delete the Players or Teams themselves (they stay
on their rosters for use in other tournaments). The button is disabled until you've
selected a tournament.

### Tables

The **Tables** row along the top shows how many tables you've added (e.g. "Tables: 4").
Click **Add Table** to add another one on the fly (useful if a table frees up from
another event, or you under-counted at creation). Assigning a table to a match in the
bracket/round view below is saved automatically as soon as you start that match — there's
no separate save step.

### Adding entrants after creation

If the tournament hasn't had any match/game actually start yet, an **Add Player:**
or **Add Team:** picker appears (whichever the tournament uses) — pick someone from
the dropdown and click **Add**. For Single/Double/Modified Single Elimination and
Round Robin, adding an entrant regenerates the whole bracket/schedule from scratch,
so do this only before play begins. Once a match has started, this row disappears —
entrants are locked in for the rest of the event.

### Playing an elimination bracket (Single/Double/Modified Single Elimination)

The bracket displays as a tree, rounds running left to right. Each match box shows
both entrants (with their seed number, if seeded) and a score field for each. If the
tournament was seeded by a rating (see [Section 4](#4-creating-a-tournament-tournament-settings-tab)), each entrant's rating for that
same system also shows next to their name - in this bracket view and on the
[Display window](#8-the-display-window). Random-draw and Team tournaments show no rating, since
none was used to seed them.

To play a match:

1. In the match's box, pick a **table** from the dropdown and click **Start**.
   - The dropdown only lists tables not currently occupied by another in-progress
     match, in numerical order (Table 1, Table 2, ...) - a table already in use won't
     appear until its match finishes.
   - You can't start a match without picking a table.
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

1. Click **Shuffle & Seat Players** once, before the first game. This randomly
   orders every player and seats them two-per-table at however many tables you
   set up; any players left over wait in the **Next Up** queue. You can click it
   again to re-shuffle (or to pick up a table you added late) right up until the
   first game is recorded — after that it's no longer available.
2. Each table shows its two current players as a card. When one of them wins,
   click the **Wins** button next to their name.
3. The loser drops one chip; the winner's chip count doesn't change and they stay
   at that table. If the loser still has chips, they go to the back of the Next
   Up queue and the next player in line takes their seat. A player who hits zero
   chips is eliminated and locks in a finishing place — their seat is filled from
   the queue instead. Once the queue is empty, any tables left with only one
   player are paired up with each other so no one waits forever.

The **Next Up** list (operator screen) always shows who's waiting and in what
order, so you know who to send to a table next. The **standings grid** updates
live after every game: finishing place, player, current chip count, games won,
and win percentage. If prize payouts are configured, a separate **Prize Payouts**
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
Works with any number of entrants (2 or more): if it isn't an exact power of two,
the top seeds get first-round byes, just like Single Elimination — those byes
carry through into the Losers bracket automatically.

### Modified Single Elimination

APA's format — a shorter, faster alternative to Double Elimination that still
guarantees everyone at least two matches. Entrants are split into **pods** of up to
8, using as few pods as possible and splitting entrants as evenly as it can across
them (16 entrants → 2 pods of 8; 20 → pods of 7, 7, 6; 24 → 3 pods of 8). A pod with
fewer than 8 entrants simply has first-round **byes**, so any field of 8 or more
works — the count no longer has to be a neat power of two. Each pod plays out
independently:

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
with no more consolation matches and no bracket reset. When the number of pods isn't
a power of two, some representatives get a bye into the next round of that final
stage. Requires at least 8 entrants (one full pod); above that, any count works.

Note: within a pod, entrants that are fewer than 8 get their round-1 byes; the byes
are spread across the pod's four round-1 matches rather than clustered.

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
number of chips. Players are shuffled into random order and seated two-per-table
at whatever tables are set up; extra players wait in a "Next Up" queue. Each
recorded game costs the loser a chip and removes them from that table — the
winner stays put, and the next player in the queue takes the loser's seat (or, if
the loser still has chips, they go to the back of the queue instead of leaving).
A player at zero chips is eliminated (their finishing place is locked in at that
moment). The last player still holding chips wins the tournament; see
[Section 6](#6-entry-fees-and-prize-payouts) for how payouts are configured, and
the standings grid for each player's chip count, games won, and win percentage.

## 8. The Display window

Click **Open Display Window** (on the Tournament tab) to open a second, read-only
window meant for a projector or a second monitor facing the room. It automatically
mirrors whatever's happening on the Tournament tab in real time — no manual refresh
needed:

- Tournament name and status.
- A "Now Playing" board showing which match is on which table.
- The live bracket (for elimination formats), Standings (Round Robin), rotation and
  money board (Ring Game), or table board, Next Up queue, and standings board (Chip
  Tournament) — completed matches' winners are highlighted.
- The **Prize Payouts** panel, if the tournament has payouts configured (see
  [Section 6](#6-entry-fees-and-prize-payouts)) — not shown for Ring Game.

Nothing in the Display window is clickable/editable — it's purely for the audience
to watch. All control stays on the main Tournament tab.

## 9. Appearance

The app automatically matches your Windows light/dark mode setting and updates
instantly if you change it in Windows — there is nothing to configure in the app
itself.
