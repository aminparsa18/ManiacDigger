namespace ManicDigger.Maui.Views;

public partial class MainMenuView : ContentPage
{
	public MainMenuView()
	{
		InitializeComponent();
	}

    private async void Button_Clicked(object sender, EventArgs e)
    {
		await Shell.Current.GoToAsync("//SinglePlayerView");
    }
}