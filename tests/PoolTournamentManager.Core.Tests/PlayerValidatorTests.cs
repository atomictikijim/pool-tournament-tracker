using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class PlayerValidatorTests
{
    private static Player ValidPlayer() => new() { FirstName = "Jane", LastName = "Doe" };

    [Fact]
    public void Validate_ReturnsNoErrors_ForMinimalValidPlayer()
    {
        var errors = PlayerValidator.Validate(ValidPlayer());

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RequiresFirstName(string firstName)
    {
        var player = ValidPlayer();
        player.FirstName = firstName;

        var errors = PlayerValidator.Validate(player);

        Assert.Contains(errors, e => e.Contains("First name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RequiresLastName(string lastName)
    {
        var player = ValidPlayer();
        player.LastName = lastName;

        var errors = PlayerValidator.Validate(player);

        Assert.Contains(errors, e => e.Contains("Last name"));
    }

    [Fact]
    public void Validate_RejectsNegativeFargoRate()
    {
        var player = ValidPlayer();
        player.FargoRate = -1;

        var errors = PlayerValidator.Validate(player);

        Assert.Contains(errors, e => e.Contains("Fargo Rating"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Validate_RejectsOutOfRangeApaEightBallSkill(int skill)
    {
        var player = ValidPlayer();
        player.ApaEightBallSkill = skill;

        var errors = PlayerValidator.Validate(player);

        Assert.Contains(errors, e => e.Contains("APA 8-Ball"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Validate_RejectsOutOfRangeApaNineBallSkill(int skill)
    {
        var player = ValidPlayer();
        player.ApaNineBallSkill = skill;

        var errors = PlayerValidator.Validate(player);

        Assert.Contains(errors, e => e.Contains("APA 9-Ball"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void Validate_AcceptsBoundaryApaSkillValues(int skill)
    {
        var player = ValidPlayer();
        player.ApaEightBallSkill = skill;
        player.ApaNineBallSkill = skill;

        var errors = PlayerValidator.Validate(player);

        Assert.Empty(errors);
    }
}
