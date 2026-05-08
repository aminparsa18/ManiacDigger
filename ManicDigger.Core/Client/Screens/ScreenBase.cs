using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using TextCopy;
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
public class ScreenBase(IGameService gameService, IOpenGlService openGlService, IAssetManager assetManager) : IScreenBase
{
    protected IGameService GameService { get; set; } = gameService;
    protected IOpenGlService OpenGlService { get; set; } = openGlService;
    protected IAssetManager AssetManager { get; set; } = assetManager;

    private Matrix4 mvMatrix = Matrix4.Identity;
    private Matrix4 pMatrix = Matrix4.Identity;
    private GeometryModel cubeModel;

    /// <summary>Fixed background tile size in pixels.</summary>
    private const int BackgroundTileSize = 512;

    /// <summary>Maximum delta-time cap per frame to avoid physics tunnelling on hitches.</summary>
    private const float MaxDeltaTime = 1f;

    private float xRot;
    private bool xInv;
    public float xSpeed;

    private float yRot;
    private bool yInv;
    public float ySpeed;

    private int overlap = 200;

    private int minspeed;
    private Random rnd;

    private int BackgroundW;
    private int BackgroundH;

    // Texture caches
    /// <summary>GPU texture cache: filename → OpenGL texture ID.</summary>
    protected Dictionary<string, int> Textures { get; set; } = [];

    /// <summary>
    /// Rasterised text texture cache: (text, fontSize) → <see cref="TextTexture"/>.
    /// Keyed by both string content and size so different sizes of the same text are cached independently.
    /// </summary>
    private readonly Dictionary<(string Text, float Size), TextTexture> textTextureCache = [];

    /// <summary>All widgets registered to this screen, in render and hit-test order.</summary>
    protected List<MenuWidget> Widgets { get; set; } = [];

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
        foreach (MenuWidget w in Widgets)
        {
            if (w.HasKeyboardFocus && (e.KeyChar == (int)Keys.Tab || e.KeyChar == (int)Keys.Enter))
            {
                if (w.Type == UIWidgetType.Button && e.KeyChar == (int)Keys.Enter)
                {
                    OnButton(w);
                    return;
                }

                if (w.NextWidget != -1)
                {
                    w.LoseFocus();
                    Widgets[w.NextWidget].GetFocus();
                    return;
                }
            }

            if (w.Type != UIWidgetType.Textbox || !w.Editing)
            {
                continue;
            }

            int key = e.KeyChar;

            if (e.CtrlPressed && key == (int)Keys.V)
            {
                Clipboard clipboardText = new Clipboard();
                if (!string.IsNullOrEmpty(clipboardText.GetText()))
                {
                    w.Text = string.Concat(w.Text, clipboardText.GetText());
                }

                return;
            }

            if (key == (int)Keys.Backspace)
            {
                if (w.Text.Length > 0)
                {
                    w.Text = w.Text[..^1];
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
        foreach (MenuWidget w in Widgets)
        {
            if (w.Type != UIWidgetType.Textbox || !w.Editing)
            {
                continue;
            }

            if (EncodingHelper.IsValidTypingChar(e.KeyChar))
            {
                w.Text = string.Concat(w.Text, (char)e.KeyChar);
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

        foreach (MenuWidget w in Widgets)
        {
            bool hit = VectorUtils.PointInRect(x, y, w.X, w.Y, w.Sizex, w.Sizey);
            w.Pressed = hit;

            if (w.Type == UIWidgetType.Textbox)
            {
                bool wasEditing = w.Editing;
                w.Editing = hit;

                if (hit && !wasEditing)
                {
                    GameService.ShowKeyboard(true);
                    editingChanged = true;
                }
                else if (!hit && wasEditing && !editingChanged)
                {
                    GameService.ShowKeyboard(false);
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
        foreach (MenuWidget w in Widgets)
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
        foreach (MenuWidget w in Widgets)
        {
            w.Pressed = false;
        }

        foreach (MenuWidget w in Widgets)
        {
            if (w.Type != UIWidgetType.Button)
            {
                continue;
            }

            if (VectorUtils.PointInRect(x, y, w.X, w.Y, w.Sizex, w.Sizey))
            {
                OnButton(w);
            }
        }
    }

    /// <summary>
    /// Updates the <see cref="MenuWidget.Hover"/> flag for every widget based on
    /// the current cursor position. Emulated mouse events are ignored unless
    /// <see cref="MouseEventArgs.GetForceUsage"/> is set.
    /// </summary>
    private void MouseMove(MouseEventArgs e)
    {
        if (e.GetEmulated() && !e.GetForceUsage())
        {
            return;
        }

        foreach (MenuWidget w in Widgets)
        {
            w.Hover = VectorUtils.PointInRect(e.GetX(), e.GetY(), w.X, w.Y, w.Sizex, w.Sizey);
        }
    }

    /// <summary>
    /// Clears the framebuffer, sets up an orthographic projection matching the
    /// current canvas size, and delegates to the active screen's <c>Render</c>.
    /// </summary>
    /// <param name="dt">Delta time forwarded to the screen renderer.</param>
    public void DrawScene(float dt)
    {
        OpenGlService.GlViewport(0, 0, GameService.CanvasWidth, GameService.CanvasHeight);
        OpenGlService.GlClearColorBufferAndDepthBuffer();
        OpenGlService.GlDisableDepthTest();
        OpenGlService.GlDisableCullFace();

        Matrix4.CreateOrthographicOffCenter(
            0, GameService.CanvasWidth,
            GameService.CanvasHeight, 0,
            0, 10,
            out pMatrix);

        Render(dt);
    }

    /// <summary>
    /// Draws all visible widgets. Rendering varies by <see cref="UIWidgetType"/>
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
        foreach (MenuWidget w in Widgets)
        {
            if (!w.Visible)
            {
                continue;
            }

            string text = w.Selected ? string.Concat("&2", w.Text) : w.Text;

            if (w.Type == UIWidgetType.Button)
            {
                DrawButton(w, text);
            }
            else if (w.Type == UIWidgetType.Textbox)
            {
                DrawTextbox(w, text);
            }
        }
    }

    /// <summary>
    /// Draws a textured button quad and, when non-empty, centres the label text over it.
    /// </summary>
    /// <param name="text">Label text. Null or empty skips text rendering.</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <param name="dx">Left edge of the button in canvas pixels.</param>
    /// <param name="dy">Top edge of the button in canvas pixels.</param>
    /// <param name="dw">Button width in pixels.</param>
    /// <param name="dh">Button height in pixels.</param>
    /// <param name="pressed">When <c>true</c>, uses the pressed/highlighted button sprite.</param>
    private void DrawButton(string text, float fontSize, float dx, float dy, float dw, float dh, bool pressed)
    {
        string textureName = pressed ? "button_sel.png" : "button.png";
        Draw2dQuad(GetTexture(textureName), dx, dy, dw, dh);

        if (!string.IsNullOrEmpty(text))
        {
            DrawText(text, fontSize, dx + (dw / 2), dy + (dh / 2), TextAlign.Center, TextBaseline.Middle);
        }
    }

    /// <summary>
    /// Renders a string at the given canvas position using a cached rasterised texture.
    /// </summary>
    /// <param name="text">The string to render.</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <param name="x">Horizontal anchor in canvas pixels.</param>
    /// <param name="y">Vertical anchor in canvas pixels.</param>
    /// <param name="align">Horizontal alignment relative to <paramref name="x"/>.</param>
    /// <param name="baseline">Vertical alignment relative to <paramref name="y"/>.</param>
    protected void DrawText(string text, float fontSize, float x, float y, TextAlign align, TextBaseline baseline)
    {
        TextTexture t = GetTextTexture(text, fontSize);

        int dx = align switch
        {
            TextAlign.Center => -t.TextWidth / 2,
            TextAlign.Right => -t.TextWidth,
            _ => 0
        };

        int dy = baseline switch
        {
            TextBaseline.Middle => -t.TextHeight / 2,
            TextBaseline.Bottom => -t.TextHeight,
            _ => 0
        };

        Draw2dQuad(t.Texture, x + dx, y + dy, t.TextureWidth, t.TextureHeight);
    }

    /// <summary>
    /// Renders a screen-aligned quad scaled to <paramref name="dw"/> × <paramref name="dh"/>
    /// and positioned at (<paramref name="dx"/>, <paramref name="dy"/>) in canvas space.
    /// The quad geometry is lazily created and reused across all calls.
    /// </summary>
    protected void Draw2dQuad(int textureid, float dx, float dy, float dw, float dh)
    {
        // Build model-view: translate → scale to size → halve → shift origin to centre
        Matrix4.CreateTranslation(dx, dy, 0, out Matrix4 translation);
        Matrix4.CreateScale(dw, dh, 0, out Matrix4 scale);
        Matrix4.CreateScale(0.5f, 0.5f, 0, out Matrix4 halfScale);
        Matrix4.CreateTranslation(1, 1, 0, out Matrix4 centreShift);

        mvMatrix = centreShift * halfScale * scale * translation;

        SetMatrixUniforms();
        cubeModel ??= OpenGlService.CreateModel(Quad.Create());
        OpenGlService.BindTexture2d(textureid);
        OpenGlService.DrawModel(cubeModel);
    }

    /// <summary>Uploads the current projection and model-view matrices to the active shader.</summary>
    private void SetMatrixUniforms()
    {
        OpenGlService.SetMatrixUniformProjection(ref pMatrix);
        OpenGlService.SetMatrixUniformModelView(ref mvMatrix);
    }

    /// <summary>
    /// Returns the OpenGL texture ID for <paramref name="name"/>, loading and caching
    /// it from the asset list on first access.
    /// </summary>
    /// <param name="name">Asset filename (case-insensitive).</param>
    protected int GetTexture(string name)
    {
        if (!Textures.TryGetValue(name, out int value))
        {
            Bitmap bmp = PixelBuffer.BitmapFromPng(GetFile(name), GetFileLength(name));
            value = OpenGlService.LoadTextureFromBitmap(bmp);
            Textures[name] = value;
            bmp.Dispose();
        }

        return value;
    }

    /// <summary>
    /// Returns the raw bytes for the named asset, or <c>null</c> if not found.
    /// Comparison is case-insensitive via <see cref="string.ToLowerInvariant"/>.
    /// </summary>
    /// <param name="name">Asset filename.</param>
    protected byte[] GetFile(string name)
    {
        string lower = name.ToLowerInvariant();
        for (int i = 0; i < AssetManager.Assets.Count; i++)
        {
            if (AssetManager.Assets[i].name == lower)
            {
                return AssetManager.Assets[i].data;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the byte length of the named asset's data, or 0 if not found.
    /// </summary>
    /// <param name="name">Asset filename.</param>
    protected int GetFileLength(string name)
    {
        string lower = name.ToLowerInvariant();
        for (int i = 0; i < AssetManager.Assets.Count; i++)
        {
            if (AssetManager.Assets[i].name == lower)
            {
                return AssetManager.Assets[i].dataLength;
            }
        }

        return 0;
    }

    /// <summary>
    /// Returns a cached <see cref="TextTexture"/> for the given text and size,
    /// rasterising and uploading a new one to the GPU if necessary.
    /// </summary>
    private TextTexture GetTextTexture(string text, float fontSize)
    {
        if (textTextureCache.TryGetValue((text, fontSize), out TextTexture cached))
        {
            return cached;
        }

        TextStyle style = new()
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = "Arial",
            Color = ColorUtils.ColorFromArgb(255, 255, 255, 255)
        };

        Bitmap textBitmap = TextColorRenderer.CreateTextTexture(style);
        int texture = OpenGlService.LoadTextureFromBitmap(textBitmap);
        TextRenderer.TextSize(text, fontSize, out int textWidth, out int textHeight);

        TextTexture entry = new()
        {
            Texture = texture,
            TextureWidth = textBitmap.Width,
            TextureHeight = textBitmap.Height,
            Text = text,
            Size = fontSize,
            TextWidth = textWidth,
            TextHeight = textHeight
        };

        textBitmap.Dispose();
        textTextureCache[(text, fontSize)] = entry;
        return entry;
    }

    /// <summary>
    /// Draws a single button widget using the style indicated by <see cref="MenuWidget.ButtonStyle"/>.
    /// </summary>
    private void DrawButton(MenuWidget w, string text)
    {
        switch (w.ButtonStyle)
        {
            case ButtonStyle.Text:
                if (w.Image != null)
                {
                    Draw2dQuad(GetTexture(w.Image), w.X, w.Y, w.Sizex, w.Sizey);
                }

                DrawText(text, w.FontSize, w.X, w.Y + (w.Sizey / 2), TextAlign.Left, TextBaseline.Middle);
                break;

            case ButtonStyle.Button:
                DrawButton(text, w.FontSize, w.X, w.Y, w.Sizex, w.Sizey, w.Hover || w.HasKeyboardFocus);
                if (w.Description != null)
                {
                    DrawText(w.Description, w.FontSize, w.X, w.Y + (w.Sizey / 2), TextAlign.Right, TextBaseline.Middle);
                }

                break;

            default:
                // Server-list entry: text is packed as five newline-separated fields:
                //   [0] server name  [1] player count  [2] map name  [3] game mode  [4] server version
                string[] fields = w.Text.Split('\n');

                if (w.Selected)
                {
                    fields[0] = string.Concat("&2", fields[0]);
                    fields[1] = string.Concat("&2", fields[1]);
                    fields[2] = string.Concat("&2", fields[2]);
                    fields[3] = string.Concat("&2", fields[3]);
                }

                DrawServerButton(fields[0], fields[1], fields[2], fields[3],
                    w.X, w.Y, w.Sizex, w.Sizey, w.Image);

                if (w.Description != null)
                {
                    // Server did not respond to the last ping — show a warning icon.
                    Draw2dQuad(GetTexture("serverlist_entry_noresponse.png"),
                        w.X - (38 * GetScale()), w.Y, w.Sizey / 2, w.Sizey / 2);
                }

                if (fields[4] != GameService.GetGameVersion())
                {
                    // Server version differs from the client — show a version-mismatch icon.
                    Draw2dQuad(GetTexture("serverlist_entry_differentversion.png"),
                        w.X - (38 * GetScale()), w.Y + (w.Sizey / 2), w.Sizey / 2, w.Sizey / 2);
                }

                break;
        }
    }

    /// <summary>
    /// Draws a server-list entry consisting of a background panel, a thumbnail icon,
    /// and four text labels (name, motd, gamemode, player count).
    /// </summary>
    public void DrawServerButton(
        string name, string motd, string gamemode, string playercount,
        float x, float y, float width, float height, string image)
    {
        // Background panel (screen-width minus margin × 64 px by convention)
        Draw2dQuad(GetTexture("serverlist_entry_background.png"), x, y, width, height);
        // Square icon on the left; height × height keeps it 1:1
        Draw2dQuad(GetTexture(image), x, y, height, height);

        //         value          size   xPos                    yPos                    align               baseline
        DrawText(name, 14, x + 70, y + 5, TextAlign.Left, TextBaseline.Top);
        DrawText(gamemode, 12, x + width - 10, y + height - 5, TextAlign.Right, TextBaseline.Bottom);
        DrawText(playercount, 12, x + width - 10, y + 5, TextAlign.Right, TextBaseline.Top);
        DrawText(motd, 12, x + 70, y + height - 5, TextAlign.Left, TextBaseline.Bottom);
    }

    /// <summary>
    /// Tiles the scrolling background texture to fill the canvas, accounting for
    /// the animated pan offset and bleed <see cref="overlap"/>.
    /// </summary>
    protected void DrawBackground()
    {
        BackgroundW = BackgroundTileSize;
        BackgroundH = BackgroundTileSize;

        int countX = (GameService.CanvasWidth + (2 * overlap)) / BackgroundW + 1;
        int countY = (GameService.CanvasHeight + (2 * overlap)) / BackgroundH + 1;

        int bgTexture = GetTexture("background.png");
        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                Draw2dQuad(
                    bgTexture,
                    (x * BackgroundW) + xRot - overlap,
                    (y * BackgroundH) + yRot - overlap,
                    BackgroundW, BackgroundH);
            }
        }
    }

    /// <summary>
    /// Returns a UI scale factor. On small/mobile screens this scales proportionally
    /// to a 1280-pixel reference width; on desktop it returns exactly 1.
    /// </summary>
    public float GetScale()
        => GameService.IsSmallScreen()
            ? GameService.CanvasWidth / 1280f
            : 1f;

    /// <summary>
    /// Draws a single textbox widget. Password fields are masked with asterisks;
    /// an underscore cursor is appended while the field is in editing mode.
    /// </summary>
    private void DrawTextbox(MenuWidget w, string text)
    {
        if (w.Password)
        {
            text = new string('*', w.Text.Length);
        }

        if (w.Editing)
        {
            text = string.Concat(text, "_");
        }

        if (w.ButtonStyle == ButtonStyle.Text)
        {
            if (w.Image != null)
            {
                Draw2dQuad(GetTexture(w.Image), w.X, w.Y, w.Sizex, w.Sizey);
            }

            DrawText(text, w.FontSize, w.X, w.Y, TextAlign.Left, TextBaseline.Top);
        }
        else
        {
            DrawButton(text, w.FontSize, w.X, w.Y, w.Sizex, w.Sizey,
                w.Hover || w.Editing || w.HasKeyboardFocus);
        }

        if (w.Description != null)
        {
            DrawText(w.Description, w.FontSize, w.X, w.Y + (w.Sizey / 2), TextAlign.Right, TextBaseline.Middle);
        }
    }

    /// <summary>
    /// Advances the background parallax scroll animation. Each axis bounces
    /// between [−overlap, +overlap] at a randomly varied speed.
    /// Delta time is clamped to <see cref="MaxDeltaTime"/> to prevent large jumps
    /// after frame hitches.
    /// </summary>
    public void Animate(float dt)
    {
        dt = Math.Min(dt, MaxDeltaTime);

        // X axis
        if (xInv)
        {
            if (xRot <= -overlap)
            {
                xInv = false;
                xSpeed = minspeed + rnd.Next(5);
            }

            xRot -= xSpeed * dt;
        }
        else
        {
            if (xRot >= overlap)
            {
                xInv = true;
                xSpeed = minspeed + rnd.Next(5);
            }

            xRot += xSpeed * dt;
        }

        // Y axis
        if (yInv)
        {
            if (yRot <= -overlap)
            {
                yInv = false;
                ySpeed = minspeed + rnd.Next(5);
            }

            yRot -= ySpeed * dt;
        }
        else
        {
            if (yRot >= overlap)
            {
                yInv = true;
                ySpeed = minspeed + rnd.Next(5);
            }

            yRot += ySpeed * dt;
        }
    }


    /// <summary>
    /// Registers a pre-loaded GPU texture in the renderer's cache under the given name,
    /// making it retrievable via <see cref="IMenuRenderer.GetTexture"/> without a
    /// redundant disk or asset lookup.
    /// </summary>
    /// <param name="name">
    /// The cache key, conventionally the asset filename (e.g. <c>"serverlist_entry_abc123.png"</c>).
    /// Must be unique; an existing entry under the same name will be overwritten.
    /// </param>
    /// <param name="textureId">The OpenGL texture ID returned by the platform after uploading the bitmap.</param>
    public void RegisterTexture(string name, int textureId) => Textures[name] = textureId;
}