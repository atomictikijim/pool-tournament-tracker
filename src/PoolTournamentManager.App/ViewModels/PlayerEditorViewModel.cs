using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

public partial class PlayerEditorViewModel : ObservableObject
{
    /// <summary>Heading shown in the modal editor window ("New Player" / "Edit Player").</summary>
    [ObservableProperty]
    private string _title = "Player";

    /// <summary>Validation errors for the current fields; null when the input is valid.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private int? _fargoRate;

    [ObservableProperty]
    private string? _tapRating;

    [ObservableProperty]
    private int? _apaEightBallSkill;

    [ObservableProperty]
    private int? _apaNineBallSkill;

    public void LoadFrom(Player player)
    {
        FirstName = player.FirstName;
        LastName = player.LastName;
        Email = player.Email;
        Phone = player.Phone;
        FargoRate = player.FargoRate;
        TapRating = player.TapRating;
        ApaEightBallSkill = player.ApaEightBallSkill;
        ApaNineBallSkill = player.ApaNineBallSkill;
    }

    public void Reset()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        Email = null;
        Phone = null;
        FargoRate = null;
        TapRating = null;
        ApaEightBallSkill = null;
        ApaNineBallSkill = null;
    }

    public void ApplyTo(Player player)
    {
        player.FirstName = FirstName;
        player.LastName = LastName;
        player.Email = Email;
        player.Phone = Phone;
        player.FargoRate = FargoRate;
        player.TapRating = TapRating;
        player.ApaEightBallSkill = ApaEightBallSkill;
        player.ApaNineBallSkill = ApaNineBallSkill;
    }

    /// <summary>
    /// Validates the current field values via <see cref="PlayerValidator"/>, populating
    /// <see cref="ErrorMessage"/> on failure. The modal editor calls this before closing so the
    /// user sees inline errors instead of committing an invalid record.
    /// </summary>
    public bool TryValidate()
    {
        var scratch = new Player { FirstName = string.Empty, LastName = string.Empty };
        ApplyTo(scratch);
        var errors = PlayerValidator.Validate(scratch);
        ErrorMessage = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
        return errors.Count == 0;
    }
}
