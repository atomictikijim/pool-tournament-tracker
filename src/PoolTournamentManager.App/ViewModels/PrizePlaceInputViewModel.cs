using CommunityToolkit.Mvvm.ComponentModel;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>One "Place N: __ %" row on the Tournament Settings create form.</summary>
public partial class PrizePlaceInputViewModel : ObservableObject
{
    public int Place { get; }

    public string PlaceLabel => Place switch
    {
        1 => "1st place",
        2 => "2nd place",
        3 => "3rd place",
        _ => $"{Place}th place"
    };

    [ObservableProperty]
    private decimal _percentage;

    public PrizePlaceInputViewModel(int place, decimal percentage = 0m)
    {
        Place = place;
        Percentage = percentage;
    }
}
