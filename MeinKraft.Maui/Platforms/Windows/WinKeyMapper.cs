#if WINDOWS
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Input;

public static class WinKeyMapper
{
    public static KeyEventArgs ToKeyEventArgs(
        Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        return new KeyEventArgs
        {
            KeyChar = MapToOpenTKKey(args.Key),
            Handled = args.Handled,
            CtrlPressed = IsKeyDown(VirtualKey.Control),
            ShiftPressed = IsKeyDown(VirtualKey.Shift),
            AltPressed = IsKeyDown(VirtualKey.Menu),   // Menu = Alt
        };
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return state.HasFlag(CoreVirtualKeyStates.Down);
    }

    private static int MapToOpenTKKey(VirtualKey key) => key switch
    {
        // Letters A-Z: VirtualKey 65-90 == OpenTK Keys 65-90 (both ASCII)
        >= VirtualKey.A and <= VirtualKey.Z => (int)key,

        // Number row 0-9: VirtualKey 48-57 == OpenTK Keys 48-57
        >= VirtualKey.Number0 and <= VirtualKey.Number9 => (int)key,

        // Numpad 0-9 → OpenTK Keys.KeyPad0(320) - KeyPad9(329)
        >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9
            => 320 + ((int)key - (int)VirtualKey.NumberPad0),

        // F1–F25: VirtualKey.F1=112, OpenTK Keys.F1=290
        >= VirtualKey.F1 and <= VirtualKey.F24
            => 290 + ((int)key - (int)VirtualKey.F1),

        // Arrow keys
        VirtualKey.Left => 263,   // OpenTK Keys.Left
        VirtualKey.Right => 262,   // OpenTK Keys.Right
        VirtualKey.Up => 265,   // OpenTK Keys.Up
        VirtualKey.Down => 264,   // OpenTK Keys.Down

        // Common keys
        VirtualKey.Space => 32,    // OpenTK Keys.Space
        VirtualKey.Enter => 257,   // OpenTK Keys.Enter
        VirtualKey.Escape => 256,   // OpenTK Keys.Escape
        VirtualKey.Tab => 258,   // OpenTK Keys.Tab
        VirtualKey.Back => 259,   // OpenTK Keys.Backspace
        VirtualKey.Insert => 260,   // OpenTK Keys.Insert
        VirtualKey.Delete => 261,   // OpenTK Keys.Delete
        VirtualKey.Home => 268,   // OpenTK Keys.Home
        VirtualKey.End => 269,   // OpenTK Keys.End
        VirtualKey.PageUp => 266,   // OpenTK Keys.PageUp
        VirtualKey.PageDown => 267,   // OpenTK Keys.PageDown

        // Modifiers (if you want to track them as keys too)
        VirtualKey.Shift => 340,   // OpenTK Keys.LeftShift
        VirtualKey.Control => 341,   // OpenTK Keys.LeftControl
        VirtualKey.Menu => 342,   // OpenTK Keys.LeftAlt

        // Numpad operators
        VirtualKey.Multiply => 332,   // OpenTK Keys.KeyPadMultiply
        VirtualKey.Add => 334,   // OpenTK Keys.KeyPadAdd
        VirtualKey.Subtract => 333,   // OpenTK Keys.KeyPadSubtract
        VirtualKey.Divide => 331,   // OpenTK Keys.KeyPadDivide
        VirtualKey.Decimal => 330,   // OpenTK Keys.KeyPadDecimal

        _ => 0   // OpenTK Keys.Unknown
    };
}
#endif