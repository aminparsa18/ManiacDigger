using OpenTK.Windowing.Common;

/// <summary>
/// Base class for all client-side mods. Override only the hooks you need.
/// </summary>
public abstract class ModBase
{
    /// <summary>Called once when the mod is loaded by the mod manager.</summary>
    public virtual void Start(IModManager modManager) { }

    /// <summary>Called each tick on the main thread; game state is read-only.</summary>
    public virtual void OnReadOnlyMainThread(Game game, float dt) { }

    /// <summary>Called each tick on a background thread; game state is read-only.</summary>
    public virtual void OnReadOnlyBackgroundThread(Game game, float dt) { }

    /// <summary>Called each tick on the main thread; game state may be modified.</summary>
    public virtual void OnReadWriteMainThread(Game game, float dt) { }

    /// <summary>Called when the player issues a client-side command. Return <see langword="true"/> to mark the command as handled.</summary>
    public virtual bool OnClientCommand(Game game, ClientCommandArgs args) => false;

    /// <summary>Called at the start of each frame.</summary>
    public virtual void OnNewFrame(Game game, float args) { }

    /// <summary>Called each fixed-timestep frame update.</summary>
    public virtual void OnNewFrameFixed(Game game, float args) { }

    /// <summary>Called during the 2D draw pass of each frame.</summary>
    public virtual void OnNewFrameDraw2d(Game game, float deltaTime) { }

    /// <summary>Called before the 3D draw pass of each frame.</summary>
    public virtual void OnBeforeNewFrameDraw3d(Game game, float deltaTime) { }

    /// <summary>Called during the 3D draw pass of each frame.</summary>
    public virtual void OnNewFrameDraw3d(Game game, float deltaTime) { }

    /// <summary>Called during the read-only main thread phase of each frame.</summary>
    public virtual void OnNewFrameReadOnlyMainThread(Game game, float deltaTime) { }

    /// <summary>Called when a keyboard key is pressed down.</summary>
    public virtual void OnKeyDown(Game game, KeyEventArgs args) { }

    /// <summary>Called when a character key is pressed (text input).</summary>
    public virtual void OnKeyPress(Game game, KeyPressEventArgs args) { }

    /// <summary>Called when a keyboard key is released.</summary>
    public virtual void OnKeyUp(Game game, KeyEventArgs args) { }

    /// <summary>Called when a mouse button is released.</summary>
    public virtual void OnMouseUp(Game game, MouseEventArgs args) { }

    /// <summary>Called when a mouse button is pressed down.</summary>
    public virtual void OnMouseDown(Game game, MouseEventArgs args) { }

    /// <summary>Called when the mouse cursor moves.</summary>
    public virtual void OnMouseMove(Game game, MouseEventArgs args) { }

    /// <summary>Called when the mouse wheel is scrolled.</summary>
    public virtual void OnMouseWheelChanged(Game game, MouseWheelEventArgs args) { }

    /// <summary>Called when a touch gesture begins.</summary>
    public virtual void OnTouchStart(Game game, TouchEventArgs e) { }

    /// <summary>Called when a touch gesture moves.</summary>
    public virtual void OnTouchMove(Game game, TouchEventArgs e) { }

    /// <summary>Called when a touch gesture ends.</summary>
    public virtual void OnTouchEnd(Game game, TouchEventArgs e) { }

    /// <summary>Called when the player uses (right-clicks) an entity.</summary>
    public virtual void OnUseEntity(Game game, OnUseEntityArgs e) { }

    /// <summary>Called when the player hits (left-clicks) an entity.</summary>
    public virtual void OnHitEntity(Game game, OnUseEntityArgs e) { }

    /// <summary>Called when the mod is unloaded. Release any resources here.</summary>
    public virtual void Dispose(Game game) { }
}