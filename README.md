# Manic Digger — OpenTK 4 Migration 

This project is based on the excellent work of the original [manicdigger/manicdigger](https://github.com/manicdigger/manicdigger) team — a multiplayer block-building voxel game inspired by Minecraft. All credit for the original game design, architecture, and content goes to them.

---
![Uploading 9c1d22eac9aac5f36bf12a5fb5c8a856.png…]()


## What's Different in This Fork

The original project was built on old OpenTK, which is no longer maintained. This fork migrates the entire client to **OpenTK 4.x**, which is built on top of GLFW and supports modern platforms and runtimes.

### Migration Highlights

#### .NET 3.5 → .NET 10 Migration
The original project targeted **.NET Framework 3.5** — a runtime from 2007 that is no longer supported on modern systems and has no cross-platform support. This fork migrates the entire solution to **.NET 10**, bringing:
- Full cross-platform support (Windows, Linux, macOS)
- Modern C# language features (pattern matching, records, nullable reference types, etc.)
- Significantly improved performance and runtime optimizations
- Active long-term support

#### OpenTK 4 Upgrade
- Replaced the old `GameWindow` constructor (which took `GraphicsMode` and display settings) with the new `GameWindowSettings` / `NativeWindowSettings` pattern
- `DisplayDevice` and `GraphicsMode` are gone — GLFW now manages context creation automatically
- VSync is now configured via `VSyncMode` on the window instance
- Unlimited framerate is set via `GameWindowSettings.UpdateFrequency = 0`

#### Input System Rewrite
- `OpenTK.Input.Key` (sequential arbitrary integers) replaced with `OpenTK.Windowing.GraphicsLibraryFramework.Keys` (GLFW USB HID key codes)
- `KeyPress` event replaced with `TextInput` event (`TextInputEventArgs.Unicode`) for proper character input handling
- Modifier key checks updated from `== KeyModifiers.X` to `HasFlag(KeyModifiers.X)` to correctly handle multiple simultaneous modifiers
- `MouseButtonEventArgs` no longer carries X/Y position — mouse position is now read from `window.MouseState.Position`
- Mouse wheel changed from `Delta`/`DeltaPrecise` to `OffsetX`/`OffsetY`
- Mouse look (captured cursor) now uses `CursorState.Grabbed` and `MouseState.Delta` instead of manual `SetPosition` centering hacks

#### Audio (OpenAL)
- `AudioContext` replaced with `ALC.OpenDevice` / `ALC.CreateContext` / `ALC.MakeContextCurrent`
- OpenAL native library now sourced via NuGet (`OpenTK.redist.openal`) instead of a bundled DLL
- Fixed a threading bug where the OpenAL context was not made current on audio worker threads, causing native crashes

#### OpenGL Compatibility Mode
The game currently runs in **OpenGL Compatibility Profile** mode. The original rendering code uses the legacy fixed-function pipeline (`GL.Begin`, `GL.Vertex`, `GL.MatrixMode`, etc.) which is not available in OpenGL Core Profile.

```csharp
Profile = ContextProfile.Compatability,
APIVersion = new Version(3, 3),
```

This keeps the game running without rewriting all rendering code at once.

---

## Roadmap

- [ ] Migrate rendering code away from the fixed-function pipeline to modern OpenGL (shaders, VAOs, VBOs)
- [ ] Switch from Compatibility Profile to Core Profile
- [ ] Full 64-bit support
- [ ] Cross-platform testing (Linux, macOS)

---

## Building

Requires **.NET 10** and **Visual Studio 2022+** or the `dotnet` CLI.

```
dotnet build
dotnet run --project ManicDigger
```

---

## Original Project

All game design, assets, server architecture, and mod API belong to the original Manic Digger project:
https://github.com/manicdigger/manicdigger

Please support and credit the original authors for their work.
