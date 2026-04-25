using OpenTK.Windowing.Common;

/// <summary>
/// Base class for all in-game menu screens. Manages a fixed-size pool of
/// <see cref="MenuWidget"/> objects and routes input events (keyboard, mouse,
/// touch) to them. Derive from this class and override <see cref="OnButton"/>
/// to respond to widget interactions.
/// </summary>
public class GameScreen : ModBase
{
    /// <summary>Reference to the current game instance.</summary>
    private IGameClient game;
    private readonly IGamePlatform platform;

    /// <summary>Maximum number of widgets this screen can hold.</summary>
    internal int WidgetCount;

    /// <summary>Widget pool. Entries are <see langword="null"/> when unused.</summary>
    internal MenuWidget[] widgets;

    /// <summary>Screen-space X origin added to all widget positions during drawing and hit-testing.</summary>
    internal int screenx;

    /// <summary>Screen-space Y origin added to all widget positions during drawing and hit-testing.</summary>
    internal int screeny;

    /// <summary>Initialises the widget pool with a capacity of 64.</summary>
    public GameScreen(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
        WidgetCount = 64;
        widgets = new MenuWidget[WidgetCount];
    }

    public void SetGame(IGameClient game)
    {
        this.game = game;
    }

    /// <inheritdoc/>
    public override void OnKeyPress(KeyPressEventArgs args) => KeyPress(args);

    /// <inheritdoc/>
    public override void OnTouchStart(TouchEventArgs e)
        => e.SetHandled(MouseDown(e.GetX(), e.GetY()));

    /// <inheritdoc/>
    public override void OnTouchEnd(TouchEventArgs e) => MouseUp(e.GetX(), e.GetY());

    /// <inheritdoc/>
    public override void OnMouseDown(MouseEventArgs args) => MouseDown(args.GetX(), args.GetY());

    /// <inheritdoc/>
    public override void OnMouseUp(MouseEventArgs args) => MouseUp(args.GetX(), args.GetY());

    /// <inheritdoc/>
    public override void OnMouseMove(MouseEventArgs args) => MouseMove(args);

    /// <summary>Called when the hardware back button is pressed. Override to handle navigation.</summary>
    public virtual void OnBackPressed() { }

    /// <summary>Called when the mouse wheel is scrolled. Override to handle scrolling.</summary>
    public virtual void OnMouseWheel(MouseWheelEventArgs e) { }

    /// <summary>
    /// Called when a button widget is clicked (mouse-up inside its bounds).
    /// Override to respond to button presses.
    /// </summary>
    /// <param name="w">The widget that was clicked.</param>
    public virtual void OnButton(MenuWidget w) { }

    /// <summary>
    /// Handles keyboard character input, routing it to whichever text-box widget
    /// is currently in editing mode. Supports backspace, tab/enter suppression,
    /// paste (Ctrl+V / key code 22), and normal printable characters.
    /// </summary>
    private void KeyPress(KeyPressEventArgs e)
    {
        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w == null || w.type != UIWidgetType.Textbox || !w.editing) { continue; }

            int key = e.KeyChar;

            if (key == 8) // backspace
            {
                if (w.text.Length > 0) { w.text = w.text[..^1]; }
                return;
            }
            if (key == 9 || key == 13) // tab, enter
            {
                return;
            }
            if (key == 22) // paste (Ctrl+V)
            {
                if (Clipboard.ContainsText()) { w.text = string.Concat(w.text, Clipboard.GetText()); }
                return;
            }
            if (platform.IsValidTypingChar(key))
            {
                w.text = string.Concat(w.text, ((char)key).ToString());
            }
        }
    }

    /// <summary>
    /// Handles a mouse or touch press at (<paramref name="x"/>, <paramref name="y"/>).
    /// Updates the <c>pressed</c> and <c>editing</c> state of all widgets and
    /// shows or hides the soft keyboard as needed.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if any widget consumed the event.
    /// </returns>
    private bool MouseDown(int x, int y)
    {
        bool handled = false;
        bool editingChange = false;

        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w == null) { continue; }

            bool hit = VectorUtils.PointInRect(x, y, screenx + w.x, screeny + w.y, w.sizex, w.sizey);

            if (w.type == UIWidgetType.Button)
            {
                w.pressed = hit;
                if (hit) { handled = true; }
            }

            if (w.type == UIWidgetType.Textbox)
            {
                w.pressed = hit;
                if (hit) { handled = true; }

                bool wasEditing = w.editing;
                w.editing = hit;

                if (w.editing && !wasEditing)
                {
                    platform.ShowKeyboard(true);
                    editingChange = true;
                }
                if (!w.editing && wasEditing && !editingChange)
                {
                    platform.ShowKeyboard(false);
                }
            }
        }

        return handled;
    }

    /// <summary>
    /// Handles a mouse or touch release at (<paramref name="x"/>, <paramref name="y"/>).
    /// Clears all pressed states and fires <see cref="OnButton"/> for any button
    /// whose bounds contain the release point.
    /// </summary>
    private void MouseUp(int x, int y)
    {
        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w != null) { w.pressed = false; }
        }

        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w == null || w.type != UIWidgetType.Button) { continue; }

            if (VectorUtils.PointInRect(x, y, screenx + w.x, screeny + w.y, w.sizex, w.sizey))
            {
                OnButton(w);
            }
        }
    }

    /// <summary>
    /// Updates the <c>hover</c> state of all widgets based on the current mouse position.
    /// Skips emulated move events unless <c>ForceUsage</c> is set.
    /// </summary>
    private void MouseMove(MouseEventArgs e)
    {
        if (e.GetEmulated() && !e.GetForceUsage()) { return; }

        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            w?.hover = VectorUtils.PointInRect(e.GetX(), e.GetY(), screenx + w.x, screeny + w.y, w.sizex, w.sizey);
        }
    }

    /// <summary>
    /// Draws all visible widgets. Buttons render as images or coloured rectangles
    /// with centred text; text boxes render their text (masked with asterisks for
    /// password fields) with a cursor appended while editing; labels render plain text.
    /// </summary>
    public void DrawWidgets()
    {
        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w == null || !w.visible) { continue; }

            string text = w.text;
            if (w.selected)
            {
                text = string.Concat(platform, "&2", text);
            }

            if (w.type == UIWidgetType.Button)
            {
                if (w.buttonStyle != ButtonStyle.Text)
                {
                    if (w.image != null)
                    {
                        game.Draw2dBitmapFile(w.image, screenx + w.x, screeny + w.y, w.sizex, w.sizey);
                    }
                    else
                    {
                        game.Draw2dTexture(game.GetOrCreateWhiteTexture(), screenx + w.x, screeny + w.y, w.sizex, w.sizey, null, 0, w.color, false);
                    }
                    game.Draw2dText1(text, screenx + (int)w.x, screeny + (int)(w.y + w.sizey / 2), (int)w.fontSize, null, false);
                }
                // ButtonStyle.Text rendering is not yet implemented.
            }

            if (w.type == UIWidgetType.Textbox)
            {
                if (w.password) { text = new string('*', w.text.Length); }
                if (w.editing) { text = string.Concat(platform, text, "_"); }
                game.Draw2dText(text, w.font, screenx + w.x, screeny + w.y, null, false);
            }

            if (w.type == UIWidgetType.Label)
            {
                game.Draw2dText(text, w.font, screenx + w.x, screeny + w.y, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
            }
        }
    }
}