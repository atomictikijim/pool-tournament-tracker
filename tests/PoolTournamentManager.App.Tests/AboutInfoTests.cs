using PoolTournamentManager.App.About;

namespace PoolTournamentManager.App.Tests;

public class AboutInfoTests
{
    [Fact]
    public void ExposesPopulatedProductMetadata()
    {
        var info = new AboutInfo();

        Assert.Equal("Pool Tournament Manager", info.AppName);
        Assert.False(string.IsNullOrWhiteSpace(info.Description));
        Assert.Contains("James Milne", info.Copyright);
        Assert.Contains("GPL-3.0-or-later", info.License);
        Assert.StartsWith("https://", info.RepositoryUrl);
    }

    [Fact]
    public void VersionDisplay_ReflectsTheAssemblyVersionInTheAppScheme()
    {
        var info = new AboutInfo();

        // Driven by <Version> in the .csproj (0.<major>[.<ui>]); should never be the "unknown"
        // fallback for a normally-built assembly.
        Assert.StartsWith("Version 0.", info.VersionDisplay);
        Assert.DoesNotContain("unknown", info.VersionDisplay);
    }
}
