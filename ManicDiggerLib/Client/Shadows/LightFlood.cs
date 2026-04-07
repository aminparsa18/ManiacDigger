
/// <summary>
/// Floods light values outward from a seed position inside a 16×16×16 chunk,
/// using a breadth-first queue to propagate light level decay neighbour by neighbour.
/// </summary>
public class LightFlood
{
    /// <summary>
    /// Initialises the flood queue with an initial capacity to avoid early reallocations.
    /// The queue is reused across <see cref="FloodLight"/> calls to avoid per-call allocation.
    /// </summary>
    public LightFlood()
    {
        _q = new Queue<int>(1024);
    }

    /// <summary>BFS queue of flat array indices pending light propagation.</summary>
    private readonly Queue<int> _q;

    /// <summary>Light level at which propagation stops.</summary>
    private const int MinLight = 0;

    // Flat array index offsets for each of the 6 face-connected neighbours.
    // Based on layout: index = (z * 16 + y) * 16 + x
    public const int XPlus = 1;
    public const int XMinus = -1;
    public const int YPlus = 16;
    public const int YMinus = -16;
    public const int ZPlus = 256;
    public const int ZMinus = -256;

    /// <summary>
    /// Converts a 3D grid coordinate into a flat array index using row-major order.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <param name="z">Layer (depth) index.</param>
    /// <param name="sizeX">Number of columns per row.</param>
    /// <param name="sizeY">Number of rows per layer.</param>
    /// <returns>The corresponding flat array index.</returns>
    private static int Index3d(int x, int y, int z, int sizeX, int sizeY)
    {
        return (z * sizeY + y) * sizeX + x;
    }

    /// <summary>
    /// Floods light outward from the given seed position within the chunk.
    /// Light decays by 1 per step and stops at opaque, non-emissive blocks or at <see cref="MinLight"/>.
    /// </summary>
    /// <param name="chunk">Flat block ID array for the chunk (16×16×16).</param>
    /// <param name="light">Flat light level array, modified in place.</param>
    /// <param name="startX">Seed X coordinate (0–15).</param>
    /// <param name="startY">Seed Y coordinate (0–15).</param>
    /// <param name="startZ">Seed Z coordinate (0–15).</param>
    /// <param name="dataLightRadius">Per-block-type light emission radius.</param>
    /// <param name="dataTransparent">Per-block-type transparency flag.</param>
    public void FloodLight(int[] chunk, byte[] light, int startX, int startY, int startZ, int[] dataLightRadius, bool[] dataTransparent)
    {
        int start = Index3d(startX, startY, startZ, 16, 16);

        // Nothing to propagate if the seed has no light.
        if (light[start] == MinLight)
            return;

        _q.Clear();
        _q.Enqueue(start);

        while (_q.Count > 0)
        {
            int vPos = _q.Dequeue();
            int vLight = light[vPos];

            if (vLight == MinLight)
                continue;

            int vBlock = chunk[vPos];

            // Skip opaque non-emissive blocks — light cannot pass through or originate here.
            if (!dataTransparent[vBlock] && dataLightRadius[vBlock] == 0)
                continue;

            int x = MapUtilCi.PosX(vPos, 16, 16);
            int y = MapUtilCi.PosY(vPos, 16, 16);
            int z = MapUtilCi.PosZ(vPos, 16, 16);

            // Propagate to each face-connected neighbour within chunk bounds.
            if (x < 15) Push(light, vLight, vPos + XPlus);
            if (x > 0) Push(light, vLight, vPos + XMinus);
            if (y < 15) Push(light, vLight, vPos + YPlus);
            if (y > 0) Push(light, vLight, vPos + YMinus);
            if (z < 15) Push(light, vLight, vPos + ZPlus);
            if (z > 0) Push(light, vLight, vPos + ZMinus);
        }
    }

    /// <summary>
    /// Enqueues a neighbour position if its current light level is more than 1 step
    /// below the current value, then updates it to <paramref name="vLight"/> - 1.
    /// </summary>
    /// <param name="light">The light array to update.</param>
    /// <param name="vLight">The current position's light level.</param>
    /// <param name="newPos">The flat array index of the neighbour to potentially update.</param>
    private void Push(byte[] light, int vLight, int newPos)
    {
        if (light[newPos] < vLight - 1)
        {
            light[newPos] = (byte)(vLight - 1);
            _q.Enqueue(newPos);
        }
    }
}
