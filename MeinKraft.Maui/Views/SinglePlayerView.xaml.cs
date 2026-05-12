namespace MeinKraft.Maui.Views;

/// <summary>Represents one save-slot entry in the list.</summary>
public class SaveSlot
{
    public string Name { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public DateTime LastPlayed { get; init; }

    public string LastPlayedText =>
        LastPlayed == DateTime.MinValue
            ? "never played"
            : $"last played  {LastPlayed:MMM dd yyyy}";
}

public partial class SinglePlayerView : ContentPage
{
    // ── Services ──────────────────────────────────────────────────────────────

    private readonly ISaveGameService _saveGameService;

    // ── Commands (bound from XAML via RelativeSource AncestorType) ────────────

    public Command<SaveSlot> LoadSlotCommand { get; }
    public Command<SaveSlot> DeleteSlotCommand { get; }

    // ── Save folder ───────────────────────────────────────────────────────────

    private static string SaveFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Manic Digger Save");

    // ── Construction ──────────────────────────────────────────────────────────

    public SinglePlayerView(ISaveGameService saveGameService)
    {
        InitializeComponent();

        _saveGameService = saveGameService;

        // Expose commands as properties so CollectionView item templates can bind
        // via RelativeSource AncestorType={x:Type ContentPage}.
        LoadSlotCommand = new Command<SaveSlot>(LoadSlot);
        DeleteSlotCommand = new Command<SaveSlot>(async slot => await DeleteSlotAsync(slot));

        BindingContext = this;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshSlotList();
    }

    // ── Slot list ─────────────────────────────────────────────────────────────

    private void RefreshSlotList()
    {
        Directory.CreateDirectory(SaveFolder);

        var slots = Directory
            .EnumerateFiles(SaveFolder, "*.*")
            .Where(f => f.EndsWith(".mddbs", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".mdss", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTime)
            .Select(path => new SaveSlot
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FilePath = path,
                LastPlayed = File.GetLastWriteTime(path),
            })
            .ToList();

        SaveSlotList.ItemsSource = slots;
        EmptyState.IsVisible = slots.Count == 0;
    }

    // ── Slot actions ──────────────────────────────────────────────────────────

    private async void LoadSlot(SaveSlot slot)
    {
        _saveGameService.InitialiseSession(SaveTarget.FromFile(slot.FilePath));
        await Shell.Current.GoToAsync("//GameView");
    }

    private async Task DeleteSlotAsync(SaveSlot slot)
    {
        bool confirmed = await DisplayAlertAsync(
            title: "Delete world?",
            message: $"\"{slot.Name}\" will be permanently deleted.",
            accept: "Delete",
            cancel: "Cancel");

        if (!confirmed) return;

        try
        {
            // Sentinel file + sibling data directory (region-file format).
            File.Delete(slot.FilePath);

            string dataDir = Path.Combine(
                Path.GetDirectoryName(slot.FilePath)!,
                Path.GetFileNameWithoutExtension(slot.FilePath));

            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Could not delete save:\n{ex.Message}", "OK");
        }

        RefreshSlotList();
    }

    // ── Button click handlers ─────────────────────────────────────────────────

    private async void OnNewWorldClicked(object sender, EventArgs e)
    {
        string defaultName = $"World {DateTime.Now:yyyyMMdd-HHmm}";

        string? name = await DisplayPromptAsync(
            title: "New World",
            message: "Enter a name for your world:",
            accept: "Create",
            cancel: "Cancel",
            placeholder: defaultName,
            maxLength: 32,
            keyboard: Keyboard.Text);

        if (name is null) return;
        if (string.IsNullOrWhiteSpace(name)) name = defaultName;

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        string path = Path.Combine(SaveFolder, name + ".mddbs");

        if (File.Exists(path))
        {
            await DisplayAlertAsync("Name taken", $"A world named \"{name}\" already exists.", "OK");
            return;
        }

        Directory.CreateDirectory(SaveFolder);
        File.WriteAllText(path, string.Empty);   // sentinel file

        _saveGameService.InitialiseSession(SaveTarget.NewGame(path));
        await Shell.Current.GoToAsync("//GameView");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainMenuView");
    }
}