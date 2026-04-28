# Fractal Brownian Motion (fBm) — World Generation Primer

## What is Noise?

Before understanding fBm, you need to understand what **noise** means in this context.

Imagine you ask a random number generator for a value at position `(x, y)`. Pure random output looks like TV static — every pixel is completely unrelated to its neighbour. That's useless for terrain because real landscapes are *smooth*: the height at one point is related to the height of nearby points.

**Gradient noise** (Perlin, Simplex, etc.) solves this. It produces smooth, continuous values where nearby points return similar results. Sample it across a 2D grid and you get gentle rolling hills:

```
0.1  0.2  0.4  0.5  0.4  ...   → smooth, but too flat and featureless
```

It looks organic, but it's missing detail. Everything is the same "blobby" scale.

---

## What is fBm?

**Fractal Brownian Motion** is the answer to the flatness problem. The idea is simple:

> *Stack multiple layers of noise on top of each other, each one twice as detailed and half as loud.*

Each layer is called an **octave**.

```
Octave 1 — Low frequency,  high amplitude  →  big continent shapes
Octave 2 — 2× frequency,  ½× amplitude    →  mountain ranges
Octave 3 — 4× frequency,  ¼× amplitude    →  hills
Octave 4 — 8× frequency,  ⅛× amplitude    →  bumps
Octave 5 — 16× frequency, 1/16× amplitude →  surface roughness
```

Add them all up and you get something that has *structure at every scale* — just like a real landscape does.

---

## The Three Knobs

Every fBm generator in this codebase has three key parameters:

### 1. Lacunarity
How much the frequency multiplies each octave. Almost always `2.0`.
- `2.0` = each octave is twice as detailed as the last
- Higher values = finer detail appears faster

### 2. Persistence
How much the amplitude multiplies each octave. Usually `0.5`.
- `0.5` = each octave is half as loud as the last
- Higher values (e.g. `0.7`) = rough, jagged terrain
- Lower values (e.g. `0.3`) = smooth, gentle terrain

### 3. Octave Count
How many layers to stack.
- More octaves = more fine detail, more CPU cost
- 4–6 is typical for terrain; 8+ for continent-scale features

```
Total cost scales linearly with octave count.
6 octaves = 6× the work of 1 octave.
```

---

## Visual Intuition

Think of it like drawing a coastline:

1. **Start with a rough shape** — a big wobbly circle (1 octave)
2. **Add medium bays and peninsulas** (2 octaves)
3. **Add smaller inlets** (3 octaves)
4. **Add rocky outcrops** (4 octaves)
5. **Add pebble-scale roughness** (5 octaves)

Each pass adds detail without destroying the overall shape. That self-similar quality at every zoom level is what makes it *fractal*.

---

## How This Codebase Uses fBm

The world generator stacks several *different* fBm modules, each tuned for a different job:

| Module | Class | Purpose | Key Settings |
|---|---|---|---|
| `continentNoise` | `Billow` | Land vs ocean mask | 8 octaves, low persistence |
| `heightRidged` | `RidgedMultifractal` | Mountain peaks | 7 octaves, high lacunarity |
| `heightSmooth` | `Perlin` | Lowland base | 5 octaves |
| `heightDetail` | `FastNoise` | Surface roughness | Fine scale overlay |
| `tempNoise` | `Perlin` | Temperature zones | 3 octaves, wide scale |
| `humidityNoise` | `Perlin` | Humidity zones | 4 octaves |
| `vegetationNoise` | `FastNoise` | Tree density | Low frequency |

They are combined like this for each tile:

```
continent  →  is this land or ocean?
    ↓
height     →  how high? (ridged inland, smooth near coast)
    ↓
temp       →  how hot? (reduced at altitude)
    ↓
humidity   →  how wet?
    ↓
DetermineBiome(height, temp, humidity) → Plains / Desert / Mountains / etc.
```

---

## The Four fBm Variants in This Codebase

### `Perlin` / `FastNoise`
Plain fBm. Signal passes through unchanged.
```
output = Σ noise(x, y, z) × amplitude
```
→ Smooth, general-purpose. Good for temperature, humidity, base height.

### `Billow` / `FastBillow`
Absolute-value fold.
```
output = Σ (2 × |noise| - 1) × amplitude
```
→ Rounded, puffy shapes. Good for continents and clouds.
Visualised: smooth noise with all valleys flipped upward into rounded hills.

### `RidgedMultifractal`
Inverted fold + feedback.
```
signal  = (1 - |noise|)²
signal *= weight_from_previous_octave
```
→ Sharp ridges with smooth valleys. The feedback loop means ridges compound — a high peak from one octave amplifies the peaks in the next. Perfect for mountain ranges.

---

## Why fBm is Expensive (and What We Did About It)

Every octave calls the underlying noise function once. With 7 octaves and a 256×256 map that's:

```
256 × 256 × 7 octaves × 3 noise calls (for ridged) = ~1.4 million noise evaluations
```

Before the float rewrite, each evaluation was doing `double` (64-bit) arithmetic. After switching to `float` (32-bit):
- Half the memory bandwidth on gradient table lookups
- SIMD-friendly (CPU can do 2 floats per instruction in many cases)
- No more silent `float → double → float` widening in the call chain

Result: LibNoise dropped from **~42% of total CPU to not appearing in the profiler at all.**

---

## Further Reading & Watching

### Videos
- 🎥 [**The Art of Code — Fractal Brownian Motion**](https://www.youtube.com/watch?v=BFld4EBO2RE)
  Excellent visual breakdown, shader-based demos
- 🎥 [**Sebastian Lague — Procedural Terrain Generation**](https://www.youtube.com/watch?v=wbpMiKiSKm8&list=PLFt_AvWsXl0eBW2EiBtl_sxmDtSgZBxB3)
  Full Unity series, highly practical, covers octaves and biomes directly
- 🎥 [**Inigo Quilez — Painting a Landscape with Maths**](https://www.youtube.com/watch?v=BFld4EBO2RE)
  More advanced, but shows exactly how fBm layers combine visually

### Articles
- 📄 [**Inigo Quilez — fBm Article**](https://iquilezles.org/articles/fbm/)
  The definitive written reference. Covers domain warping, derivatives, and variants
- 📄 [**Inigo Quilez — Ridged Noise**](https://iquilezles.org/articles/morenoise/)
  Explains the ridged and billow variants with live shader demos
- 📄 [**Red Blob Games — Noise Functions**](https://www.redblobgames.com/maps/terrain-from-noise/)
  Interactive, beginner-friendly. Lets you tweak octaves live in the browser
- 📄 [**Ken Perlin's Original Paper**](https://mrl.cs.nyu.edu/~perlin/paper445.pdf)
  The source. Short and worth reading once you're comfortable with the basics

### Interactive Tools
- 🛠️ [**FastNoise Lite Previewer**](https://auburn.github.io/FastNoiseLite/)
  Real-time noise previewer — change octaves, lacunarity, persistence and see results instantly
- 🛠️ [**Book of Shaders — Noise Chapter**](https://thebookofshaders.com/13/)
  Interactive GLSL playground, great for building intuition
