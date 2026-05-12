// OverlayMenuView.xaml.cs
// ════════════════════════
// Code-behind for the pause / options overlay ContentView.
//
// Responsibilities of THIS file:
//   • Internal panel navigation (Pause ↔ Options)
//   • Toggle / stepper state for every option row
//   • Raising ReturnToGameRequested / ExitToMenuRequested so that
//     GameView can do cursor release, game-state changes, and Shell nav
//     without this view knowing about those services.
//
// GameView's responsibility:
//   • Subscribe to the two events below in its constructor
//   • Call ShowPauseMenu() when ESC is pressed
//   • Control IsVisible on this ContentView

namespace MeinKraft.Maui.Views;

public partial class OverlayMenuView : ContentView
{
    // ── Events raised for GameView to handle ─────────────────────────────────
    // OverlayMenuView never touches _game or _gameWindowService directly.
    // GameView subscribes to these in its constructor and owns those concerns.

    /// <summary>
    /// Raised when the player clicks "Return to Game".
    /// GameView should: hide this overlay, recapture cursor, restore GameState.Normal.
    /// </summary>
    public event EventHandler? ReturnToGameRequested;

    /// <summary>
    /// Raised when the player clicks "Exit to Menu".
    /// GameView should: hide this overlay, release cursor, Shell.GoToAsync("//MainMenuView").
    /// </summary>
    public event EventHandler? ExitToMenuRequested;

    // ── Resolution stepper ────────────────────────────────────────────────────
    private static readonly string[] Resolutions =
    {
        "1280×720",
        "1366×768",
        "1600×900",
        "1920×1080",
        "2560×1440",
        "3840×2160",
    };

    private int _resolutionIndex = 3; // default: 1920×1080

    // ── Backing option state ──────────────────────────────────────────────────
    // Initialise from your settings store / IGame as needed.
    private bool _smoothShadows = true;
    private bool _darkenSides = true;
    private bool _fullscreen = false;
    private bool _serverTextures = true;
    private bool _sound = true;
    private bool _autoJump = true;

    public OverlayMenuView()
    {
        InitializeComponent();
        ApplyAllToggleStates();
    }

    // ── Public API called by GameView ─────────────────────────────────────────

    /// <summary>
    /// Resets the overlay to the Pause panel (not Options).
    /// GameView calls this before setting IsVisible = true.
    /// </summary>
    public void ShowPauseMenu()
    {
        PausePanel.IsVisible = true;
        OptionsPanel.IsVisible = false;
    }

    // ── Internal panel navigation ─────────────────────────────────────────────

    private void OnReturnToGameClicked(object sender, EventArgs e)
        => ReturnToGameRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitToMenuClicked(object sender, EventArgs e)
        => ExitToMenuRequested?.Invoke(this, EventArgs.Empty);

    private void OnOptionsClicked(object sender, EventArgs e)
    {
        PausePanel.IsVisible = false;
        OptionsPanel.IsVisible = true;
    }

    private void OnOptionsBackClicked(object sender, EventArgs e)
    {
        OptionsPanel.IsVisible = false;
        PausePanel.IsVisible = true;
    }

    // ── Toggle visual helper ──────────────────────────────────────────────────

    /// <summary>
    /// Swaps the active/inactive style between an ON/OFF button pair.
    /// Green = active segment, dim stone = inactive segment.
    /// </summary>
    private void SetToggle(Button btnOn, Button btnOff, bool value)
    {
        btnOn.Style = value
            ? (Style)Resources["ToggleBtnActive"]
            : (Style)Resources["ToggleBtnInactive"];

        btnOff.Style = value
            ? (Style)Resources["ToggleBtnInactive"]
            : (Style)Resources["ToggleBtnActive"];
    }

    private void ApplyAllToggleStates()
    {
        SetToggle(BtnSmoothOn, BtnSmoothOff, _smoothShadows);
        SetToggle(BtnDarkenOn, BtnDarkenOff, _darkenSides);
        SetToggle(BtnFullscreenOn, BtnFullscreenOff, _fullscreen);
        SetToggle(BtnServerTexOn, BtnServerTexOff, _serverTextures);
        SetToggle(BtnSoundOn, BtnSoundOff, _sound);
        SetToggle(BtnAutoJumpOn, BtnAutoJumpOff, _autoJump);
        LblResolution.Text = Resolutions[_resolutionIndex];
    }

    // ── Option handlers ───────────────────────────────────────────────────────

    private void OnSmoothShadowsOnClicked(object sender, EventArgs e)
    {
        _smoothShadows = true;
        SetToggle(BtnSmoothOn, BtnSmoothOff, true);
        // TODO: _game.Config3d.SmoothShadows = true; _game.ShouldRedrawAllBlocks = true;
    }

    private void OnSmoothShadowsOffClicked(object sender, EventArgs e)
    {
        _smoothShadows = false;
        SetToggle(BtnSmoothOn, BtnSmoothOff, false);
        // TODO: _game.Config3d.SmoothShadows = false; _game.ShouldRedrawAllBlocks = true;
    }

    private void OnDarkenSidesOnClicked(object sender, EventArgs e)
    {
        _darkenSides = true;
        SetToggle(BtnDarkenOn, BtnDarkenOff, true);
        // TODO: _game.Config3d.DarkenSides = true; _game.ShouldRedrawAllBlocks = true;
    }

    private void OnDarkenSidesOffClicked(object sender, EventArgs e)
    {
        _darkenSides = false;
        SetToggle(BtnDarkenOn, BtnDarkenOff, false);
        // TODO: _game.Config3d.DarkenSides = false; _game.ShouldRedrawAllBlocks = true;
    }

    private void OnFullscreenOnClicked(object sender, EventArgs e)
    {
        _fullscreen = true;
        SetToggle(BtnFullscreenOn, BtnFullscreenOff, true);
        // TODO: platform fullscreen on
    }

    private void OnFullscreenOffClicked(object sender, EventArgs e)
    {
        _fullscreen = false;
        SetToggle(BtnFullscreenOn, BtnFullscreenOff, false);
        // TODO: platform fullscreen off
    }

    private void OnServerTexturesOnClicked(object sender, EventArgs e)
    {
        _serverTextures = true;
        SetToggle(BtnServerTexOn, BtnServerTexOff, true);
        // TODO: _game.UseServerTextures = true;
    }

    private void OnServerTexturesOffClicked(object sender, EventArgs e)
    {
        _serverTextures = false;
        SetToggle(BtnServerTexOn, BtnServerTexOff, false);
        // TODO: _game.UseServerTextures = false;
    }

    private void OnSoundOnClicked(object sender, EventArgs e)
    {
        _sound = true;
        SetToggle(BtnSoundOn, BtnSoundOff, true);
        // TODO: _game.AudioEnabled = true;
    }

    private void OnSoundOffClicked(object sender, EventArgs e)
    {
        _sound = false;
        SetToggle(BtnSoundOn, BtnSoundOff, false);
        // TODO: _game.AudioEnabled = false;
    }

    private void OnAutoJumpOnClicked(object sender, EventArgs e)
    {
        _autoJump = true;
        SetToggle(BtnAutoJumpOn, BtnAutoJumpOff, true);
        // TODO: _game.AutoJumpEnabled = true;
    }

    private void OnAutoJumpOffClicked(object sender, EventArgs e)
    {
        _autoJump = false;
        SetToggle(BtnAutoJumpOn, BtnAutoJumpOff, false);
        // TODO: _game.AutoJumpEnabled = false;
    }

    // ── Resolution stepper ────────────────────────────────────────────────────

    private void OnResolutionPrevClicked(object sender, EventArgs e)
    {
        _resolutionIndex = (_resolutionIndex - 1 + Resolutions.Length) % Resolutions.Length;
        LblResolution.Text = Resolutions[_resolutionIndex];
        // TODO: apply resolution change via _gameWindowService
    }

    private void OnResolutionNextClicked(object sender, EventArgs e)
    {
        _resolutionIndex = (_resolutionIndex + 1) % Resolutions.Length;
        LblResolution.Text = Resolutions[_resolutionIndex];
        // TODO: apply resolution change via _gameWindowService
    }
}