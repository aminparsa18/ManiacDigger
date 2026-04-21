using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Renders and handles interaction for the inventory screen, including the main
/// item grid, wear-place slots, material selector bar, drag-drop feedback, and
/// item tooltips.
/// </summary>
public class ModGuiInventory : ModBase
{
    /// <summary>Reference to the current game instance.</summary>
    internal Game game;

    /// <summary>Item data helpers (texture IDs, sizes, display info).</summary>
    internal InventoryUtils dataItems;

    /// <summary>Client-side inventory query utilities (cell lookups, area checks).</summary>
    internal InventoryUtilClient inventoryUtil;

    /// <summary>Controller that translates UI clicks into inventory packets.</summary>
    internal IInventoryController controller;

    /// <summary>Pixel size of one inventory cell in both axes.</summary>
    internal int CellDrawSize;

    /// <summary>Number of cells visible per page horizontally.</summary>
    private readonly int _cellCountInPageX;

    /// <summary>Number of cells visible per page vertically.</summary>
    private readonly int _cellCountInPageY;

    /// <summary>Total cell columns in the full inventory grid.</summary>
    private readonly int _cellCountTotalX;

    /// <summary>Total cell rows across all pages of the inventory grid.</summary>
    private readonly int _cellCountTotalY;

    /// <summary>
    /// Screen-space X origins of each wear-place slot, indexed by <see cref="WearPlace"/>.
    /// Relative to <see cref="InventoryStartX"/>.
    /// </summary>
    private readonly Point[] _wearPlaceStart;

    /// <summary>
    /// Cell dimensions (columns × rows) of each wear-place slot, indexed by <see cref="WearPlace"/>.
    /// </summary>
    private readonly Point[] _wearPlaceCells;

    /// <summary>Number of wear-place entries (length of <see cref="_wearPlaceStart"/>).</summary>
    private const int WearPlaceCount = 5;

    /// <summary>First row of the inventory currently scrolled into view.</summary>
    internal int ScrollLine;

    /// <summary>
    /// Timestamp (ms) when the scroll-up button was last pressed for auto-scroll.
    /// <c>0</c> means not scrolling.
    /// </summary>
    private int _scrollingUpTimeMs;

    /// <summary>
    /// Timestamp (ms) when the scroll-down button was last pressed for auto-scroll.
    /// <c>0</c> means not scrolling.
    /// </summary>
    private int _scrollingDownTimeMs;

    /// <summary>Initialises cell dimensions and wear-place layout data.</summary>
    public ModGuiInventory()
    {
        // Wear-place slot origins (relative to inventory background image origin).
        _wearPlaceStart = new Point[WearPlaceCount];
        _wearPlaceStart[(int)WearPlace.RightHand] = new Point(34, 100);
        _wearPlaceStart[(int)WearPlace.MainArmor] = new Point(74, 100);
        _wearPlaceStart[(int)WearPlace.Boots] = new Point(194, 100);
        _wearPlaceStart[(int)WearPlace.Helmet] = new Point(114, 100);
        _wearPlaceStart[(int)WearPlace.Gauntlet] = new Point(154, 100);

        // All wear-place slots are 1×1 cells.
        _wearPlaceCells = new Point[WearPlaceCount];
        for (int i = 0; i < WearPlaceCount; i++) { _wearPlaceCells[i] = new Point(1, 1); }

        _cellCountInPageX = 12;
        _cellCountInPageY = 7;
        _cellCountTotalX = 12;
        _cellCountTotalY = 7 * 6;
        CellDrawSize = 40;
    }

    /// <summary>Returns the screen X coordinate of the inventory background's left edge.</summary>
    public int InventoryStartX() => game.Width() / 2 - 560 / 2;

    /// <summary>Returns the screen Y coordinate of the inventory background's top edge.</summary>
    public int InventoryStartY() => game.Height() / 2 - 600 / 2;

    /// <summary>Returns the screen X coordinate of the top-left inventory cell.</summary>
    public int CellsStartX() => 33 + InventoryStartX();

    /// <summary>Returns the screen Y coordinate of the top-left inventory cell.</summary>
    public int CellsStartY() => 180 + InventoryStartY();

    /// <summary>Returns the pixel size of one material-selector cell at the current UI scale.</summary>
    public int ActiveMaterialCellSize() => (int)(48 * game.Scale());

    private int MaterialSelectorStartX() => (int)(MaterialSelectorBgStartX() + 17 * game.Scale());
    private int MaterialSelectorStartY() => (int)(MaterialSelectorBgStartY() + 17 * game.Scale());
    private int MaterialSelectorBgStartX() => (int)(game.Width() / 2 - 512 / 2 * game.Scale());
    private int MaterialSelectorBgStartY() => (int)(game.Height() - 90 * game.Scale());

    private int ScrollButtonSize() => CellDrawSize;
    private int ScrollUpButtonX() => CellsStartX() + _cellCountInPageX * CellDrawSize;
    private int ScrollUpButtonY() => CellsStartY();
    private int ScrollDownButtonX() => CellsStartX() + _cellCountInPageX * CellDrawSize;
    private int ScrollDownButtonY() => CellsStartY() + (_cellCountInPageY - 1) * CellDrawSize;

    /// <inheritdoc/>
    public override void OnKeyPress(Game game_, KeyPressEventArgs args)
    {
        if (game.guistate != GuiState.Inventory) { return; }

        // Key codes 49–57 = '1'–'9', 48 = '0' → material slots 0–9.
        int keyChar = args.KeyChar;
        if (keyChar >= 49 && keyChar <= 57) { game.ActiveMaterial = keyChar - 49; }
        if (keyChar == 48) { game.ActiveMaterial = 9; }
    }

    /// <inheritdoc/>
    public override void OnMouseDown(Game game_, MouseEventArgs args)
    {
        if (game.guistate != GuiState.Inventory) { return; }

        Point mouse = new(args.GetX(), args.GetY());

        // Material selector bar click.
        int? materialSlot = SelectedMaterialSelectorSlot(mouse);
        if (materialSlot != null)
        {
            game.ActiveMaterial = materialSlot.Value;
            controller.InventoryClick(new Packet_InventoryPosition
            {
                Type = PacketInventoryPositionType.MaterialSelector,
                MaterialId = game.ActiveMaterial
            });
            args.SetHandled(true);
            return;
        }

        // Main grid click.
        Point? cell = SelectedCell(mouse);
        if (cell != null)
        {
            Packet_InventoryPosition mainClick = new()
            {
                Type = PacketInventoryPositionType.MainArea,
                AreaX = cell.Value.X,
                AreaY = cell.Value.Y + ScrollLine
            };

            if (args.GetButton() == (int)MouseButton.Left)
            {
                controller.InventoryClick(mainClick);
            }
            else
            {
                // Right-click: pick up → equip to right hand → put back.
                controller.InventoryClick(mainClick);
                controller.InventoryClick(new Packet_InventoryPosition
                {
                    Type = PacketInventoryPositionType.WearPlace,
                    WearPlace = (int)WearPlace.RightHand,
                    ActiveMaterial = game.ActiveMaterial
                });
                controller.InventoryClick(mainClick);
            }

            if (game.guistate == GuiState.Inventory)
            {
                args.SetHandled(true);
            }
            return;
        }

        // Wear-place slot click.
        int? wearPlace = SelectedWearPlace(mouse);
        if (wearPlace != null)
        {
            controller.InventoryClick(new Packet_InventoryPosition
            {
                Type = PacketInventoryPositionType.WearPlace,
                WearPlace = wearPlace.Value,
                ActiveMaterial = game.ActiveMaterial
            });
            args.SetHandled(true);
            return;
        }

        // Scroll-up button.
        if (HitTest(mouse, ScrollUpButtonX(), ScrollUpButtonY(), ScrollButtonSize(), ScrollButtonSize()))
        {
            ScrollUp();
            _scrollingUpTimeMs = game.platform.TimeMillisecondsFromStart;
            args.SetHandled(true);
            return;
        }

        // Scroll-down button.
        if (HitTest(mouse, ScrollDownButtonX(), ScrollDownButtonY(), ScrollButtonSize(), ScrollButtonSize()))
        {
            ScrollDown();
            _scrollingDownTimeMs = game.platform.TimeMillisecondsFromStart;
            args.SetHandled(true);
            return;
        }

        game.GuiStateBackToGame();
    }

    /// <inheritdoc/>
    public override void OnTouchStart(Game game_, TouchEventArgs e)
    {
        MouseEventArgs args = new();
        args.SetX(e.GetX());
        args.SetY(e.GetY());
        OnMouseDown(game_, args);
        e.SetHandled(args.GetHandled());
    }

    /// <inheritdoc/>
    public override void OnMouseUp(Game game_, MouseEventArgs args)
    {
        if (game != null && game.guistate != GuiState.Inventory) { return; }
        _scrollingUpTimeMs = 0;
        _scrollingDownTimeMs = 0;
    }

    /// <inheritdoc/>
    public override void OnMouseWheelChanged(Game game_, MouseWheelEventArgs args)
    {
        float delta = args.OffsetY;
        bool shiftHeld = game_.keyboardState[game_.GetKey(Keys.LeftShift)];

        bool inNormalOrOutsideCells = game_.guistate == GuiState.Normal
            || (game_.guistate == GuiState.Inventory && !IsMouseOverCells());

        if (inNormalOrOutsideCells && !shiftHeld)
        {
            game_.ActiveMaterial = ((game_.ActiveMaterial - (int)delta) % 10 + 10) % 10;
        }

        if (IsMouseOverCells() && game.guistate == GuiState.Inventory)
        {
            if (delta > 0) { ScrollUp(); }
            if (delta < 0) { ScrollDown(); }
        }
    }

    /// <inheritdoc/>
    public override void OnNewFrameDraw2d(Game game_, float deltaTime)
    {
        game = game_;

        if (dataItems == null)
        {
            dataItems = new InventoryUtils(game_);
            controller = ClientInventoryController.Create(game_);
            inventoryUtil = game.d_InventoryUtil;
        }

        if (game.guistate == GuiState.MapLoading) { return; }

        DrawMaterialSelector();

        if (game.guistate != GuiState.Inventory) { return; }

        AdvanceAutoScroll();

        Point mouse = new(game.mouseCurrentX, game.mouseCurrentY);

        game.Draw2dBitmapFile("inventory.png", InventoryStartX(), InventoryStartY(), 1024, 1024);

        DrawInventoryItems();
        DrawDragDropFeedback(mouse);
        DrawMaterialSelector();
        DrawWearPlaceItems();
        DrawTooltips(mouse);

        if (game.d_Inventory.DragDropItem != null)
        {
            DrawItem(mouse.X, mouse.Y, game.d_Inventory.DragDropItem, 0, 0);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the mouse cursor is over the inventory
    /// cell grid or its adjacent scroll buttons.
    /// </summary>
    public bool IsMouseOverCells()
        => SelectedCellOrScrollbar(game.mouseCurrentX, game.mouseCurrentY);

    /// <summary>Scrolls the inventory grid up by one row, clamped to row 0.</summary>
    public void ScrollUp()
    {
        if (ScrollLine > 0) { ScrollLine--; }
    }

    /// <summary>Scrolls the inventory grid down by one row, clamped to the last page.</summary>
    public void ScrollDown()
    {
        int max = _cellCountTotalY - _cellCountInPageY;
        if (ScrollLine < max) { ScrollLine++; }
    }

    /// <summary>
    /// Draws the material selector bar at the bottom of the screen and highlights
    /// the active material slot.
    /// </summary>
    public void DrawMaterialSelector()
    {
        game.Draw2dBitmapFile("materials.png",
            MaterialSelectorBgStartX(), MaterialSelectorBgStartY(),
            (int)(1024 * game.Scale()), (int)(128 * game.Scale()));

        int startX = MaterialSelectorStartX();
        int startY = MaterialSelectorStartY();
        int cellSize = ActiveMaterialCellSize();

        for (int i = 0; i < 10; i++)
        {
            Packet_Item item = game.d_Inventory.RightHand[i];
            if (item != null)
            {
                DrawItem(startX + i * cellSize, startY, item, cellSize, cellSize);
            }
        }

        game.Draw2dBitmapFile("activematerial.png",
            startX + cellSize * game.ActiveMaterial,
            startY,
            cellSize * 64 / 48,
            cellSize * 64 / 48);
    }

    /// <summary>
    /// Draws a single inventory item at the given screen position.
    /// Block items render as a terrain texture tile with an optional count label.
    /// Other item classes render as a bitmap graphic.
    /// </summary>
    /// <param name="screenposX">Left edge of the drawing area.</param>
    /// <param name="screenposY">Top edge of the drawing area.</param>
    /// <param name="item">Item to draw. No-op when <see langword="null"/>.</param>
    /// <param name="drawsizeX">Override draw width in pixels, or 0 to use cell-sized default.</param>
    /// <param name="drawsizeY">Override draw height in pixels, or 0 to use cell-sized default.</param>
    private void DrawItem(int screenposX, int screenposY, Packet_Item item, int drawsizeX, int drawsizeY)
    {
        if (item == null) { return; }

        int sizex = dataItems.ItemSizeX(item);
        int sizey = InventoryUtils.ItemSizeY(item);

        if (drawsizeX == 0 || drawsizeX == -1)
        {
            drawsizeX = CellDrawSize * sizex;
            drawsizeY = CellDrawSize * sizey;
        }

        if (item.ItemClass == Packet_ItemClassEnum.Block)
        {
            if (item.BlockId == 0) { return; }

            game.Draw2dTexture(game.terrainTexture, screenposX, screenposY,
                drawsizeX, drawsizeY,
                dataItems.TextureIdForInventory()[item.BlockId],
                Game.                TexturesPacked, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);

            if (item.BlockCount > 1)
            {
                game.Draw2dText(item.BlockCount.ToString(),
                    new Font("Arial", 8),
                    screenposX, screenposY, null, false);
            }
        }
        else
        {
            game.Draw2dBitmapFile(InventoryUtils.ItemGraphics(item), screenposX, screenposY, drawsizeX, drawsizeY);
        }
    }

    /// <summary>
    /// Draws a tooltip popup for <paramref name="item"/> near the given screen position,
    /// repositioning it if it would overflow the screen edges.
    /// </summary>
    public void DrawItemInfo(int screenposX, int screenposY, Packet_Item item)
    {
        int sizex = dataItems.ItemSizeX(item);
        int sizey = InventoryUtils.ItemSizeY(item);

        game.platform.TextSize(dataItems.ItemInfo(item), 11.5f, out int tw, out int th);
        tw += 6;
        th += 4;

        int w = tw + CellDrawSize * sizex;
        int h = th < CellDrawSize * sizey + 4 ? CellDrawSize * sizey + 4 : th;

        screenposX = Math.Clamp(screenposX, w + 20, game.Width() - (w + 20));
        screenposY = Math.Clamp(screenposY, h + 20, game.Height() - (h + 20));

        // Black border, grey fill.
        game.Draw2dTexture(game.WhiteTexture(), screenposX - w, screenposY - h, w, h, null, 0, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
        game.Draw2dTexture(game.WhiteTexture(), screenposX - w + 2, screenposY - h + 2, w - 4, h - 4, null, 0, ColorUtils.ColorFromArgb(255, 105, 105, 105), false);

        game.Draw2dText(dataItems.ItemInfo(item),
            new Font("Arial", 10),
            screenposX - tw + 4, screenposY - h + 2, null, false);

        DrawItem(screenposX - w + 2, screenposY - h + 2, new Packet_Item { BlockId = item.BlockId }, 0, 0);
    }

    // Private helpers — drawing sub-sections

    /// <summary>Draws all items currently visible in the scrolled inventory grid.</summary>
    private void DrawInventoryItems()
    {
        for (int i = 0; i < game.d_Inventory.ItemsCount; i++)
        {
            Packet_PositionItem k = game.d_Inventory.Items[i];
            if (k == null) { continue; }

            int screenRow = k.Y - ScrollLine;
            if (screenRow >= 0 && screenRow < _cellCountInPageY)
            {
                DrawItem(CellsStartX() + k.X * CellDrawSize, CellsStartY() + screenRow * CellDrawSize, k.Value_, 0, 0);
            }
        }
    }

    /// <summary>
    /// Draws a coloured overlay on the cell or wear-place slot under the cursor
    /// while an item is being dragged, indicating whether the drop is valid
    /// (green) or blocked (red).
    /// </summary>
    private void DrawDragDropFeedback(Point mouse)
    {
        if (game.d_Inventory.DragDropItem == null) { return; }

        Point? cellInPage = SelectedCell(mouse);
        if (cellInPage != null)
        {
            int sizex = dataItems.ItemSizeX(game.d_Inventory.DragDropItem);
            int sizey = InventoryUtils.ItemSizeY(game.d_Inventory.DragDropItem);

            if (cellInPage.Value.X + sizex <= _cellCountInPageX
             && cellInPage.Value.Y + sizey <= _cellCountInPageY)
            {
                var itemsAtArea = inventoryUtil.ItemsAtArea(
                    cellInPage.Value.X, cellInPage.Value.Y + ScrollLine, sizex, sizey);

                int color = (itemsAtArea == null || itemsAtArea.Count > 1)
                    ? ColorUtils.ColorFromArgb(100, 255, 0, 0)   // red — blocked
                    : ColorUtils.ColorFromArgb(100, 0, 255, 0);  // green — free

                game.Draw2dTexture(game.WhiteTexture(),
                    cellInPage.Value.X * CellDrawSize + CellsStartX(),
                    cellInPage.Value.Y * CellDrawSize + CellsStartY(),
                    CellDrawSize * sizex, CellDrawSize * sizey,
                    null, 0, color, false);
            }
        }

        int? wearSlot = SelectedWearPlace(mouse);
        if (wearSlot != null)
        {
            Point origin = new(_wearPlaceStart[wearSlot.Value].X + InventoryStartX(),
                               _wearPlaceStart[wearSlot.Value].Y + InventoryStartY());
            Point cells = _wearPlaceCells[wearSlot.Value];

            int color = InventoryUtils.CanWear((WearPlace)wearSlot.Value, game.d_Inventory.DragDropItem)
                ? ColorUtils.ColorFromArgb(100, 0, 255, 0)   // green — can equip
                : ColorUtils.ColorFromArgb(100, 255, 0, 0);  // red — cannot equip

            game.Draw2dTexture(game.WhiteTexture(),
                origin.X, origin.Y,
                CellDrawSize * cells.X, CellDrawSize * cells.Y,
                null, 0, color, false);
        }
    }

    /// <summary>Draws the item currently equipped in each wear-place slot.</summary>
    private void DrawWearPlaceItems()
    {
        DrawWearItem(WearPlace.RightHand, game.d_Inventory.RightHand[game.ActiveMaterial]);
        DrawWearItem(WearPlace.MainArmor, game.d_Inventory.MainArmor);
        DrawWearItem(WearPlace.Boots, game.d_Inventory.Boots);
        DrawWearItem(WearPlace.Helmet, game.d_Inventory.Helmet);
        DrawWearItem(WearPlace.Gauntlet, game.d_Inventory.Gauntlet);
    }

    /// <summary>Draws a single wear-place item at its configured slot origin.</summary>
    private void DrawWearItem(WearPlace place, Packet_Item item)
    {
        int idx = (int)place;
        DrawItem(_wearPlaceStart[idx].X + InventoryStartX(),
                 _wearPlaceStart[idx].Y + InventoryStartY(),
                 item, 0, 0);
    }

    /// <summary>
    /// Draws item tooltips for whichever cell, wear-place slot, or material-selector
    /// slot is currently under the mouse cursor.
    /// </summary>
    private void DrawTooltips(Point mouse)
    {
        Point? cell = SelectedCell(mouse);
        if (cell != null)
        {
            Point scrolledCell = new(cell.Value.X, cell.Value.Y + ScrollLine);
            Point? itemOrigin = inventoryUtil.ItemAtCell(scrolledCell);
            if (itemOrigin != null)
            {
                Packet_Item item = GetItem(game.d_Inventory, itemOrigin.Value.X, itemOrigin.Value.Y);
                if (item != null) { DrawItemInfo(mouse.X, mouse.Y, item); }
            }
        }

        int? wearSlot = SelectedWearPlace(mouse);
        if (wearSlot != null)
        {
            Packet_Item item = inventoryUtil.ItemAtWearPlace((WearPlace)wearSlot.Value, game.ActiveMaterial);
            if (item != null) { DrawItemInfo(mouse.X, mouse.Y, item); }
        }

        int? matSlot = SelectedMaterialSelectorSlot(mouse);
        if (matSlot != null)
        {
            Packet_Item item = game.d_Inventory.RightHand[matSlot.Value];
            if (item != null) { DrawItemInfo(mouse.X, mouse.Y, item); }
        }
    }

    /// <summary>
    /// Advances the auto-scroll that triggers when the user holds down a scroll button.
    /// Fires every 250 ms while the button is held.
    /// </summary>
    private void AdvanceAutoScroll()
    {
        int now = game.platform.TimeMillisecondsFromStart;
        if (_scrollingUpTimeMs != 0 && now - _scrollingUpTimeMs > 250)
        {
            _scrollingUpTimeMs = now;
            ScrollUp();
        }
        if (_scrollingDownTimeMs != 0 && now - _scrollingDownTimeMs > 250)
        {
            _scrollingDownTimeMs = now;
            ScrollDown();
        }
    }

    // Private helpers — hit testing

    /// <summary>
    /// Returns the inventory cell under <paramref name="mouse"/>, or
    /// <see langword="null"/> when the cursor is outside the grid.
    /// </summary>
    private Point? SelectedCell(Point mouse)
    {
        if (mouse.X < CellsStartX() || mouse.Y < CellsStartY()
         || mouse.X > CellsStartX() + _cellCountInPageX * CellDrawSize
         || mouse.Y > CellsStartY() + _cellCountInPageY * CellDrawSize)
        {
            return null;
        }
        return new Point((mouse.X - CellsStartX()) / CellDrawSize,
                         (mouse.Y - CellsStartY()) / CellDrawSize);
    }

    /// <summary>
    /// Returns <see langword="true"/> when (<paramref name="mx"/>, <paramref name="my"/>)
    /// is over the cell grid or the adjacent scroll buttons.
    /// </summary>
    private bool SelectedCellOrScrollbar(int mx, int my)
        => mx >= CellsStartX()
        && my >= CellsStartY()
        && mx <= CellsStartX() + (_cellCountInPageX + 1) * CellDrawSize
        && my <= CellsStartY() + _cellCountInPageY * CellDrawSize;

    /// <summary>
    /// Returns the wear-place index under <paramref name="mouse"/>, or
    /// <see langword="null"/> when the cursor is outside all wear-place slots.
    /// </summary>
    private int? SelectedWearPlace(Point mouse)
    {
        for (int i = 0; i < WearPlaceCount; i++)
        {
            int px = _wearPlaceStart[i].X + InventoryStartX();
            int py = _wearPlaceStart[i].Y + InventoryStartY();
            int pw = _wearPlaceCells[i].X * CellDrawSize;
            int ph = _wearPlaceCells[i].Y * CellDrawSize;
            if (HitTest(mouse, px, py, pw, ph)) { return i; }
        }
        return null;
    }

    /// <summary>
    /// Returns the material-selector slot index under <paramref name="mouse"/>,
    /// or <see langword="null"/> when the cursor is outside the bar.
    /// </summary>
    private int? SelectedMaterialSelectorSlot(Point mouse)
    {
        int cellSize = ActiveMaterialCellSize();
        int startX = MaterialSelectorStartX();
        int startY = MaterialSelectorStartY();

        if (mouse.X >= startX && mouse.Y >= startY
         && mouse.X < startX + 10 * cellSize
         && mouse.Y < startY + cellSize)
        {
            return (mouse.X - startX) / cellSize;
        }
        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="mouse"/> lies within the
    /// rectangle defined by (<paramref name="x"/>, <paramref name="y"/>, <paramref name="w"/>, <paramref name="h"/>).
    /// </summary>
    private static bool HitTest(Point mouse, int x, int y, int w, int h)
        => mouse.X >= x && mouse.Y >= y && mouse.X < x + w && mouse.Y < y + h;

    /// <summary>
    /// Returns the item at the given grid coordinates, or <see langword="null"/>
    /// when no item occupies that position.
    /// </summary>
    private static Packet_Item GetItem(Packet_Inventory inventory, int x, int y)
    {
        for (int i = 0; i < inventory.ItemsCount; i++)
        {
            if (inventory.Items[i].X == x && inventory.Items[i].Y == y)
            {
                return inventory.Items[i].Value_;
            }
        }
        return null;
    }
}