using MemoryPack;

namespace MeinKraft;

/// <summary>
/// Describes a server-sent dialog window, including its dimensions,
/// modality, and the collection of widgets it contains.
/// </summary>
[MemoryPackable]
public partial class Dialog
{
    /// <summary>
    /// The widgets (buttons, labels, text boxes, etc.) displayed inside this dialog.
    /// </summary>
    public Widget[] Widgets { get; set; }

    /// <summary>Width of the dialog window in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Height of the dialog window in pixels.</summary>
    public int Height { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the dialog blocks all input to the game
    /// until it is dismissed.
    /// </summary>
    public bool IsModal { get; set; }
}