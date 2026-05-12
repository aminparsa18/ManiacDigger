using MemoryPack;

namespace MeinKraft;

/// <summary>
/// Describes the font used to render text within a server-sent dialog widget.
/// </summary>
[MemoryPackable]
public partial class DialogFont
{
    /// <summary>Initialises a <see cref="DialogFont"/> with default values (Verdana 11pt, regular).</summary>
    public DialogFont() { }

    /// <summary>Initialises a <see cref="DialogFont"/> with explicit font properties.</summary>
    /// <param name="familyName">Font family name (e.g. <c>"Verdana"</c>, <c>"Arial"</c>).</param>
    /// <param name="size">Font size in points.</param>
    /// <param name="fontStyle">Style flags (bold, italic, etc.).</param>
    [MemoryPackConstructor]
    public DialogFont(string familyName, float size, DialogFontStyle fontStyle)
    {
        FamilyName = familyName;
        Size = size;
        FontStyle = fontStyle;
    }

    /// <summary>Font family name. Defaults to <c>"Verdana"</c>.</summary>
    public string FamilyName { get; set; } = "Verdana";

    /// <summary>Font size in points. Defaults to <c>11</c>.</summary>
    public float Size { get; set; } = 11f;

    /// <summary>Font style flags (regular, bold, italic, etc.).</summary>
    public DialogFontStyle FontStyle { get; set; }
}