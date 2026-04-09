using OpenTK.Windowing.Common;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Base class for all main-menu screens. Manages a list of <see cref="MenuWidget"/>
/// objects and routes keyboard, mouse and touch input events to them.
/// </summary>
/// <remarks>
/// Derive from this class and override <see cref="OnButton"/> to respond to
/// widget interactions. Override <see cref="Render"/> to draw screen-specific
/// content on top of the shared widget layer.
/// </remarks>
public class ScreenBase
{
    /// <summary>Reference to the owning <see cref="MainMenu"/>.</summary>
    internal MainMenu menu;

    /// <summary>All widgets registered to this screen, in render and hit-test order.</summary>
    internal List<MenuWidget> widgets = new();

    /// <summary>Called once per frame. Override to render screen-specific content.</summary>
    /// <param name="dt">Elapsed time in seconds since the previous frame.</param>
    public virtual void Render(float dt) { }

    public virtual void OnKeyDown(KeyEventArgs e) => KeyDown(e);
    public virtual void OnKeyPress(KeyPressEventArgs e) => KeyPress(e);
    public virtual void OnKeyUp(KeyEventArgs e) { }
    public virtual void OnTouchStart(TouchEventArgs e) => MouseDown(e.GetX(), e.GetY());
    public virtual void OnTouchMove(TouchEventArgs e) { }
    public virtual void OnTouchEnd(TouchEventArgs e) => MouseUp(e.GetX(), e.GetY());
    public virtual void OnMouseDown(MouseEventArgs e) => MouseDown(e.GetX(), e.GetY());
    public virtual void OnMouseUp(MouseEventArgs e) => MouseUp(e.GetX(), e.GetY());
    public virtual void OnMouseMove(MouseEventArgs e) => MouseMove(e);

    /// <summary>Called when the hardware back button is pressed. Override to handle navigation.</summary>
    public virtual void OnBackPressed() { }

    /// <summary>Called when the mouse wheel is scrolled. Override to handle scrolling.</summary>
    public virtual void OnMouseWheel(MouseWheelEventArgs e) { }

    /// <summary>Called when translations are reloaded. Override to refresh localised widget text.</summary>
    public virtual void LoadTranslations() { }

    /// <summary>
    /// Called when a button widget is activated (clicked or Enter-pressed while focused).
    /// Override to respond to button interactions.
    /// </summary>
    /// <param name="w">The widget that was activated.</param>
    public virtual void OnButton(MenuWidget w) { }

    /// <summary>
    /// Routes a key-down event to whichever widget currently holds keyboard focus.
    /// Handles Tab/Enter navigation between widgets, Enter activation of focused
    /// buttons, Ctrl+V paste, and Backspace deletion in textboxes.
    /// </summary>
    private void KeyDown(KeyEventArgs e)
    {
        foreach (MenuWidget w in widgets)
        {
            if (w.hasKeyboardFocus && (e.KeyChar == (int)Keys.Tab || e.KeyChar == (int)Keys.Enter))
            {
                if (w.type == WidgetType.Button && e.KeyChar == (int)Keys.Enter)
                {
                    OnButton(w);
                    return;
                }
                if (w.nextWidget != -1)
                {
                    w.LoseFocus();
                    widgets[w.nextWidget].GetFocus();
                    return;
                }
            }

            if (w.type != WidgetType.Textbox || !w.editing) { continue; }

            int key = e.KeyChar;

            if (e.CtrlPressed && key == (int)Keys.V)
            {
                if (Clipboard.ContainsText())
                {
                    w.text = string.Concat(w.text, Clipboard.GetText());
                }
                return;
            }

            if (key == (int)Keys.Backspace)
            {
                if (w.text.Length > 0)
                {
                    w.text = w.text[..^1];
                }
                return;
            }
        }
    }

    /// <summary>
    /// Routes a printable character to any textbox that is currently in editing mode.
    /// Characters are filtered through <c>IsValidTypingChar</c> before being appended.
    /// </summary>
    private void KeyPress(KeyPressEventArgs e)
    {
        foreach (MenuWidget w in widgets)
        {
            if (w.type != WidgetType.Textbox || !w.editing) { continue; }

            if (menu.p.IsValidTypingChar(e.KeyChar))
            {
                w.text = string.Concat(w.text, (char)e.KeyChar);
            }
        }
    }

    /// <summary>
    /// Handles a pointer-down event at (<paramref name="x"/>, <paramref name="y"/>).
    /// Updates pressed/editing state and shows or hides the software keyboard when
    /// a textbox gains or loses editing focus.
    /// </summary>
    private void MouseDown(int x, int y)
    {
        bool editingChanged = false;

        foreach (MenuWidget w in widgets)
        {
            bool hit = VectorUtils.PointInRect(x, y, w.x, w.y, w.sizex, w.sizey);
            w.pressed = hit;

            if (w.type == WidgetType.Textbox)
            {
                bool wasEditing = w.editing;
                w.editing = hit;

                if (hit && !wasEditing)
                {
                    menu.p.ShowKeyboard(true);
                    editingChanged = true;
                }
                else if (!hit && wasEditing && !editingChanged)
                {
                    menu.p.ShowKeyboard(false);
                }
            }

            if (hit)
            {
                AllLoseFocus();
                w.GetFocus();
            }
        }
    }

    /// <summary>Removes keyboard focus from every widget.</summary>
    private void AllLoseFocus()
    {
        foreach (MenuWidget w in widgets)
        {
            w.LoseFocus();
        }
    }

    /// <summary>
    /// Handles a pointer-up event at (<paramref name="x"/>, <paramref name="y"/>).
    /// Clears all pressed states, then fires <see cref="OnButton"/> for any button
    /// whose bounds contain the release point.
    /// </summary>
    private void MouseUp(int x, int y)
    {
        foreach (MenuWidget w in widgets)
        {
            w.pressed = false;
        }

        foreach (MenuWidget w in widgets)
        {
            if (w.type != WidgetType.Button) { continue; }

            if (VectorUtils.PointInRect(x, y, w.x, w.y, w.sizex, w.sizey))
            {
                OnButton(w);
            }
        }
    }

    /// <summary>
    /// Updates the <see cref="MenuWidget.hover"/> flag for every widget based on
    /// the current cursor position. Emulated mouse events are ignored unless
    /// <see cref="MouseEventArgs.GetForceUsage"/> is set.
    /// </summary>
    private void MouseMove(MouseEventArgs e)
    {
        if (e.GetEmulated() && !e.GetForceUsage()) { return; }

        foreach (MenuWidget w in widgets)
        {
            w.hover = VectorUtils.PointInRect(e.GetX(), e.GetY(), w.x, w.y, w.sizex, w.sizey);
        }
    }

    /// <summary>
    /// Draws all visible widgets. Rendering varies by <see cref="WidgetType"/>
    /// and <see cref="ButtonStyle"/>:
    /// <list type="bullet">
    ///   <item><description><see cref="ButtonStyle.Text"/> — draws an optional icon then text.</description></item>
    ///   <item><description><see cref="ButtonStyle.Button"/> — draws a button with optional right-aligned description.</description></item>
    ///   <item><description>Server-entry style — draws a four-line server card with optional warning overlays.</description></item>
    ///   <item><description>Textboxes — masks password fields, appends a cursor when editing.</description></item>
    /// </list>
    /// </summary>
    public void DrawWidgets()
    {
        foreach (MenuWidget w in widgets)
        {
            if (!w.visible) { continue; }

            string text = w.selected ? string.Concat("&2", w.text) : w.text;

            if (w.type == WidgetType.Button)
            {
                DrawButton(w, text);
            }
            else if (w.type == WidgetType.Textbox)
            {
                DrawTextbox(w, text);
            }
        }
    }

    /// <summary>
    /// Draws a single button widget using the style indicated by <see cref="MenuWidget.buttonStyle"/>.
    /// </summary>
    private void DrawButton(MenuWidget w, string text)
    {
        switch (w.buttonStyle)
        {
            case ButtonStyle.Text:
                if (w.image != null)
                {
                    menu.Draw2dQuad(menu.GetTexture(w.image), w.x, w.y, w.sizex, w.sizey);
                }
                menu.DrawText(text, w.fontSize, w.x, w.y + w.sizey / 2, TextAlign.Left, TextBaseline.Middle);
                break;

            case ButtonStyle.Button:
                menu.DrawButton(text, w.fontSize, w.x, w.y, w.sizex, w.sizey, w.hover || w.hasKeyboardFocus);
                if (w.description != null)
                {
                    menu.DrawText(w.description, w.fontSize, w.x, w.y + w.sizey / 2, TextAlign.Right, TextBaseline.Middle);
                }
                break;

            default:
                // Server-list entry: text is packed as five newline-separated fields:
                //   [0] server name  [1] player count  [2] map name  [3] game mode  [4] server version
                string[] fields = w.text.Split('\n');

                if (w.selected)
                {
                    fields[0] = string.Concat("&2", fields[0]);
                    fields[1] = string.Concat("&2", fields[1]);
                    fields[2] = string.Concat("&2", fields[2]);
                    fields[3] = string.Concat("&2", fields[3]);
                }

                menu.DrawServerButton(fields[0], fields[1], fields[2], fields[3],
                    w.x, w.y, w.sizex, w.sizey, w.image);

                if (w.description != null)
                {
                    // Server did not respond to the last ping — show a warning icon.
                    menu.Draw2dQuad(menu.GetTexture("serverlist_entry_noresponse.png"),
                        w.x - 38 * menu.GetScale(), w.y, w.sizey / 2, w.sizey / 2);
                }

                if (fields[4] != menu.p.GetGameVersion())
                {
                    // Server version differs from the client — show a version-mismatch icon.
                    menu.Draw2dQuad(menu.GetTexture("serverlist_entry_differentversion.png"),
                        w.x - 38 * menu.GetScale(), w.y + w.sizey / 2, w.sizey / 2, w.sizey / 2);
                }
                break;
        }
    }

    /// <summary>
    /// Draws a single textbox widget. Password fields are masked with asterisks;
    /// an underscore cursor is appended while the field is in editing mode.
    /// </summary>
    private void DrawTextbox(MenuWidget w, string text)
    {
        if (w.password) { text = new string('*', w.text.Length); }
        if (w.editing) { text = string.Concat(text, "_"); }

        if (w.buttonStyle == ButtonStyle.Text)
        {
            if (w.image != null)
            {
                menu.Draw2dQuad(menu.GetTexture(w.image), w.x, w.y, w.sizex, w.sizey);
            }
            menu.DrawText(text, w.fontSize, w.x, w.y, TextAlign.Left, TextBaseline.Top);
        }
        else
        {
            menu.DrawButton(text, w.fontSize, w.x, w.y, w.sizex, w.sizey,
                w.hover || w.editing || w.hasKeyboardFocus);
        }

        if (w.description != null)
        {
            menu.DrawText(w.description, w.fontSize, w.x, w.y + w.sizey / 2, TextAlign.Right, TextBaseline.Middle);
        }
    }
}