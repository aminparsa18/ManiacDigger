
/// <summary>
/// Client mod that drives the main render loop by forwarding the scheduler's
/// <c>OnReadOnlyMainThread</c> callback to <see cref="Game.MainThreadOnRenderFrame"/>.
/// </summary>
/// <remarks>
/// Positioning this as a mod — rather than calling <c>MainThreadOnRenderFrame</c>
/// directly from the scheduler — keeps render dispatch consistent with the rest
/// of the mod lifecycle and allows other mods registered before it to run their
/// own <c>OnReadOnlyMainThread</c> hooks first.
/// </remarks>
public class ModDrawMain : ModBase
{
    /// <inheritdoc/>
    public override void OnReadOnlyMainThread(Game game, float dt)
    {
        game.MainThreadOnRenderFrame(dt);
    }
}
