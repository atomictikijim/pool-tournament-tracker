using System;
using System.Linq;
using PoolTournamentManager.App.Help;

namespace PoolTournamentManager.App.Tests;

public class HelpContentProviderTests
{
    [Theory]
    [InlineData(HelpTopic.Players)]
    [InlineData(HelpTopic.Teams)]
    [InlineData(HelpTopic.Tournament)]
    [InlineData(HelpTopic.TournamentSettings)]
    public void For_ReturnsANonEmptyDocumentForEveryTab(HelpTopic topic)
    {
        var document = HelpContentProvider.For(topic);

        Assert.False(string.IsNullOrWhiteSpace(document.Title));
        Assert.NotEmpty(document.Blocks);
        // Every tab's guide should open with a top-level heading, and no block may be blank.
        Assert.Equal(HelpBlockKind.Heading, document.Blocks[0].Kind);
        Assert.All(document.Blocks, block => Assert.False(string.IsNullOrWhiteSpace(block.Text)));
    }

    [Fact]
    public void For_CoversEveryDefinedHelpTopic()
    {
        // Guards against adding a HelpTopic without wiring up its content (the switch would
        // otherwise fall through to the empty default document).
        foreach (HelpTopic topic in Enum.GetValues<HelpTopic>())
        {
            var document = HelpContentProvider.For(topic);
            Assert.NotEmpty(document.Blocks);
        }
    }
}
