using System;
using System.Reflection;

namespace PoolTournamentManager.App.About;

/// <summary>
/// The read-only facts shown in the About window. The version is read from the running assembly
/// (driven by &lt;Version&gt; in the .csproj) so it can't drift out of sync with the build, and is
/// formatted to the app's "0.&lt;major&gt;[.&lt;ui&gt;]" scheme. Everything else is static product
/// metadata; keep the copyright/license lines in step with the LICENSE file and README.
/// </summary>
public sealed class AboutInfo
{
    public string AppName => "Pool Tournament Manager";

    public string VersionDisplay { get; }

    public string Description =>
        "A Windows desktop application for running pool tournaments — persistent player and team " +
        "rosters; single, double and modified single elimination, round robin, ring game and chip " +
        "tournament formats; live match operation with entry fees and prize payouts; and a " +
        "read-only second-screen display for the room.";

    public string Copyright => "Copyright © 2026 James Milne";

    public string License =>
        "Licensed under the GNU General Public License, version 3 or later (GPL-3.0-or-later).";

    public string CommercialNote =>
        "A separate commercial license for proprietary use is available — contact " +
        "james.milne@prolocity.com.";

    public string RepositoryUrl => "https://github.com/atomictikijim/pool-tournament-tracker";

    public string Runtime => $".NET {Environment.Version.ToString(3)} · WPF";

    public AboutInfo()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionDisplay = version is null
            ? "Version unknown"
            : version.Build > 0
                ? $"Version {version.Major}.{version.Minor}.{version.Build}"
                : $"Version {version.Major}.{version.Minor}";
    }
}
