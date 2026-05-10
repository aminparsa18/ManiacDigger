namespace ManicDigger.Maui.Views;

public partial class SinglePlayerView : ContentPage
{
	public SinglePlayerView()
	{
		InitializeComponent();
	}

    private async void Button_Clicked(object sender, EventArgs e)
    {
		await Shell.Current.GoToAsync("//GameView");
    }
}