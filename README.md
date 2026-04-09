# Manic Digger — OpenTK 4 Migration 

[![Screenshot](https://raw.githubusercontent.com/manicdigger/manicdigger-screenshots/master/9c1d22eac9aac5f36bf12a5fb5c8a856.png)](https://raw.githubusercontent.com/manicdigger/manicdigger-screenshots/master/9c1d22eac9aac5f36bf12a5fb5c8a856.png) [![Screenshot](https://raw.githubusercontent.com/manicdigger/manicdigger-screenshots/master/2014-12-25_22-38-53.png)](https://raw.githubusercontent.com/manicdigger/manicdigger-screenshots/master/2014-12-25_22-38-53.png)

This project is based on the excellent work of the original [manicdigger/manicdigger](https://github.com/manicdigger/manicdigger) team — a multiplayer block-building voxel game inspired by Minecraft. All credit for the original game design, architecture, and content goes to them.

---

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

---

### Additional Migration Work (this fork)

#### Math Library Migration
- Custom `Vec3` / `Mat4` classes replaced with `System.Numerics.Vector3` and `System.Numerics.Matrix4x4`
- Introduced `ToOpenTK()` conversion helpers (`Matrix4x4` → `OpenTK.Mathematics.Matrix4`, `Vector3` → `OpenTK.Mathematics.Vector3`) since the legacy GL pipeline still requires OpenTK math types for calls like `GL.LoadMatrix`
- Fixed a column-major ordering bug: `System.Numerics.Matrix4x4` is row-major, but OpenGL's fixed-function pipeline expects column-major — the converter transposes accordingly

#### Custom Cursor
- `window.Cursor = new MouseCursor(hotx, hoty, sizex, sizey, data)` rewritten for OpenTK 4's `ICursorHandle` system
- Pixel format corrected from RGBA to BGRA (premultiplied alpha) as required by the new API

#### VSync Context Timing Fix
- `VSync` assignment moved inside the window constructor / `OnLoad` — setting it before the OpenGL context is fully initialized caused a runtime crash ("Cannot set swap interval without a current OpenGL context")

#### OpenAL Lifecycle & Threading
- `AudioContext` replaced with the explicit `ALC.OpenDevice` → `ALC.CreateContext` → `ALC.MakeContextCurrent` pattern, with proper `ALC.DestroyContext` / `ALC.CloseDevice` on shutdown
- Fixed a threading bug where OpenAL audio worker threads were calling `AL.*` functions without first making the context current on that thread, causing native crashes

---

### Modern OpenGL Migration (VAOs / VBOs / Shaders)

The original rendering code used OpenGL's legacy fixed-function pipeline exclusively (`GL.Begin`, `GL.Vertex`, `GL.MatrixMode`, `GL.LoadMatrix`, display lists, client-side arrays, etc.). This fork replaces all of it with a fully modern OpenGL rendering path.

#### What was replaced

- **Display lists** (`GL.GenLists`, `GL.NewList`, `GL.CallList`) → VAO + VBO upload via `CreateModel`
- **Client-side arrays** (`GL.EnableClientState`, `GL.VertexPointer`, `GL.ColorPointer`, `GL.TexCoordPointer`) → `GL.VertexAttribPointer` with persistent GPU buffers
- **Fixed-function matrix stack** (`GL.MatrixMode`, `GL.LoadMatrix`, `GL.Ortho`, `GL.Frustum`) → CPU-side matrix stack using `OpenTK.Mathematics.Matrix4`, uploaded to shaders as uniforms
- **Fixed-function lighting** (`GL.LightModel`, `GL.Enable(Lighting)`, `GL.ColorMaterial`) → ambient light uniform in the fragment shader
- **Fixed-function fog** (`GL.Fog`, `GL.Enable(Fog)`) → exp² fog implemented in the fragment shader
- **Alpha testing** (`GL.AlphaFunc`, `GL.Enable(AlphaTest)`) → `discard` in the fragment shader
- **`EnableCap.Texture2D`** — removed entirely; textures are always available in modern OpenGL

#### VAO / VBO strategy

`ModelData` (the game's universal geometry container) was extended with GPU handles (`vaoId`, `vertexVboId`, `colorVboId`, `uvVboId`, `indexVboId`). Three upload paths cover all use cases:

- **`CreateModel`** — first-time upload of static geometry (terrain chunks, quad model, etc.)
- **`UpdateModel`** — full re-upload of all buffers for fully dynamic geometry rebuilt every frame (cuboids, animated models)
- **`UpdateModelColors`** — partial re-upload of only the color buffer for geometry where only vertex colors change per frame (sky sphere sun/glow blending)

#### Shader

A single GLSL 3.30 core shader pair handles all rendering:

```glsl
// vertex shader inputs
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColor;    // normalised from byte RGBA
layout(location = 2) in vec2 aUv;

// fragment shader uniforms
uniform sampler2D uTexture;
uniform bool      uUseTexture;   // false for vertex-colored geometry (sky sphere)
uniform vec3      uAmbientLight;
uniform bool      uFogEnabled;
uniform vec4      uFogColor;
uniform float     uFogDensity;   // exp² fog
uniform mat4      uProjection;
uniform mat4      uModelView;
```

`uUseTexture` is toggled automatically by `BindTexture2d` — binding texture 0 switches to vertex-color-only mode, which is used by the sky sphere and other untextured geometry.

#### Namespace

Migrated from `OpenTK.Graphics.OpenGL` to `OpenTK.Graphics.OpenGL4`. The old namespace exposes deprecated fixed-function symbols; the new one only exposes the OpenGL 4.x API surface, so the compiler catches any remaining legacy calls.

---

## Roadmap

- [x] Migrate rendering code away from the fixed-function pipeline to modern OpenGL (shaders, VAOs, VBOs)
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