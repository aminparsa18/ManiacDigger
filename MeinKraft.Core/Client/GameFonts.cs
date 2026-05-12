/// <summary>
/// Single source of truth for all font usage across the game.
/// </summary>
public static class GameFonts
{
    /// <summary>MAUI alias registered in MauiProgram.ConfigureFonts.</summary>
    public const string Family = "GameFont";

    /// <summary>Default size for HUD / chat text.</summary>
    public const float SizeDefault = 14f;

    /// <summary>Small labels, coordinates, debug overlays.</summary>
    public const float SizeSmall = 10f;

    /// <summary>Large titles, death screen, menu headers.</summary>
    public const float SizeLarge = 20f;

    public static readonly TextFont Default = new(Family, SizeDefault);
    public static readonly TextFont Small = new(Family, SizeSmall);
    public static readonly TextFont Large = new(Family, SizeLarge);
}