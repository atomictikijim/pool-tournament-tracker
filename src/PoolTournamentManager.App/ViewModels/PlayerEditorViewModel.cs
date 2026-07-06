using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.App.ViewModels;

public partial class PlayerEditorViewModel : ObservableObject
{
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
    private string? _usaplRating;

    [ObservableProperty]
    private string? _tapRating;

    [ObservableProperty]
    private int? _apaEightBallSkill;

    [ObservableProperty]
    private int? _apaNineBallSkill;

    [ObservableProperty]
    private bool _isActive = true;

    public void LoadFrom(Player player)
    {
        FirstName = player.FirstName;
        LastName = player.LastName;
        Email = player.Email;
        Phone = player.Phone;
        FargoRate = player.FargoRate;
        UsaplRating = player.UsaplRating;
        TapRating = player.TapRating;
        ApaEightBallSkill = player.ApaEightBallSkill;
        ApaNineBallSkill = player.ApaNineBallSkill;
        IsActive = player.IsActive;
    }

    public void Reset()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        Email = null;
        Phone = null;
        FargoRate = null;
        UsaplRating = null;
        TapRating = null;
        ApaEightBallSkill = null;
        ApaNineBallSkill = null;
        IsActive = true;
    }

    public void ApplyTo(Player player)
    {
        player.FirstName = FirstName;
        player.LastName = LastName;
        player.Email = Email;
        player.Phone = Phone;
        player.FargoRate = FargoRate;
        player.UsaplRating = UsaplRating;
        player.TapRating = TapRating;
        player.ApaEightBallSkill = ApaEightBallSkill;
        player.ApaNineBallSkill = ApaNineBallSkill;
        player.IsActive = IsActive;
    }
}
