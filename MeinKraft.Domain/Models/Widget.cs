using MemoryPack;

namespace MeinKraft;

/// <summary>
/// A single UI element within a server-sent <see cref="Dialog"/>.
/// Widgets can be buttons, labels, text boxes, or images depending on <see cref="Type"/>.
/// </summary>
[MemoryPackable]
public partial class Widget
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Unique identifier for this widget within the dialog.
    /// Sent back to the server when the widget is clicked.
    /// </summary>
    public string Id { get; set; }

    /// <summary>Widget kind (image, text, text box, button, etc.).</summary>
    public WidgetType Type { get; set; }

    // ── Layout ────────────────────────────────────────────────────────────────

    /// <summary>X position in pixels relative to the dialog's top-left corner.</summary>
    public int X { get; set; }

    /// <summary>Y position in pixels relative to the dialog's top-left corner.</summary>
    public int Y { get; set; }

    /// <summary>Width of the widget in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Height of the widget in pixels.</summary>
    public int Height { get; set; }

    // ── Content ───────────────────────────────────────────────────────────────

    /// <summary>Text content displayed by or entered into this widget.</summary>
    public string Text { get; set; }

    /// <summary>
    /// Asset name of the image to display.
    /// Use <see cref="SolidImage"/> for a filled colour rectangle.
    /// </summary>
    public string Image { get; set; }

    /// <summary>ARGB colour applied to this widget. Defaults to <c>-1</c> (white).</summary>
    public int Color { get; set; } = -1;

    /// <summary>Font used to render this widget's text.</summary>
    public DialogFont Font { get; set; }

    // ── Interaction ───────────────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/>, this widget is clickable and sends an event
    /// to the server when activated.
    /// </summary>
    public bool Click { get; set; }

    /// <summary>
    /// Keyboard shortcut that triggers this widget as if it were clicked.
    /// <c>'\0'</c> means no shortcut is assigned.
    /// </summary>
    public char ClickKey { get; set; }

    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Special image name that renders a solid filled rectangle instead of a
    /// texture asset. Used with <see cref="MakeSolid"/>.
    /// </summary>
    public const string SolidImage = "Solid";

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a solid filled rectangle widget at the given position and size.
    /// </summary>
    /// <param name="x">X position in pixels.</param>
    /// <param name="y">Y position in pixels.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="color">ARGB fill colour.</param>
    public static Widget MakeSolid(float x, float y, float width, float height, int color) =>
        new()
        {
            Type = WidgetType.Image,
            Image = SolidImage,
            X = (int)x,
            Y = (int)y,
            Width = (int)width,
            Height = (int)height,
            Color = color,
        };

    /// <summary>
    /// Creates a non-interactive text label widget.
    /// </summary>
    /// <param name="text">Text to display.</param>
    /// <param name="font">Font used to render the text.</param>
    /// <param name="x">X position in pixels.</param>
    /// <param name="y">Y position in pixels.</param>
    /// <param name="textColor">ARGB text colour.</param>
    public static Widget MakeText(string text, DialogFont font, float x, float y, int textColor) =>
        new()
        {
            Type = WidgetType.Text,
            Text = text,
            X = (int)x,
            Y = (int)y,
            Font = font,
            Color = textColor,
        };

    /// <summary>
    /// Creates an editable text box widget.
    /// </summary>
    /// <param name="text">Initial text content.</param>
    /// <param name="font">Font used to render the text.</param>
    /// <param name="x">X position in pixels.</param>
    /// <param name="y">Y position in pixels.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="textColor">ARGB text colour.</param>
    public static Widget MakeTextBox(string text, DialogFont font,
        float x, float y, float width, float height, int textColor) =>
        new()
        {
            Type = WidgetType.TextBox,
            Text = text,
            X = (int)x,
            Y = (int)y,
            Width = (int)width,
            Height = (int)height,
            Font = font,
            Color = textColor,
        };
}