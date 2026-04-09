/// <summary>
/// Holds the CPU-side geometry buffers and GPU handle references for a single renderable mesh.
/// Geometry is described by parallel arrays of positions (<see cref="Xyz"/>), texture coordinates
/// (<see cref="Uv"/>), and vertex colours (<see cref="Rgba"/>), indexed by <see cref="Indices"/>.
/// GPU handles (<see cref="VaoId"/>, <see cref="VertexVboId"/> etc.) are populated by
/// <see cref="IGamePlatform.CreateModel"/> and must not be modified directly.
/// </summary>
public class ModelData
{
    // GPU handles (set by CreateModel)
    public int VaoId { get; set; }
    public int VertexVboId { get; set; }
    public int ColorVboId { get; set; }
    public int UvVboId { get; set; }
    public int IndexVboId { get; set; }

    // geometry
    public float[] Xyz { get; set; }
    public float[] Uv { get; set; }
    public byte[] Rgba { get; set; }
    public int[] Indices { get; set; }
    public int Mode { get; set; }

    // counts
    public int VerticesCount { get; set; }
    public int IndicesCount { get; set; }

    // computed counts
    public int XyzCount => VerticesCount * 3;
    public int UvCount => VerticesCount * 2;
    public int RgbaCount => VerticesCount * 4;

    /// Maximum vertex capacity derived from the allocated xyz array size
    public int VerticesMax => Xyz.Length / 3;

    /// Maximum index capacity derived from the allocated indices array size.
    public int IndicesMax => Indices.Length;
}


public class ModelDataTool
{
    public static void AddVertex(ModelData model, float x, float y, float z, float u, float v, int color)
    {
        if (model.VerticesCount >= model.Xyz.Length / 3)
        {
            var xyz = model.Xyz;
            var uv = model.Uv;
            var rgba = model.Rgba;

            Array.Resize(ref xyz, xyz.Length * 2);
            Array.Resize(ref uv, uv.Length * 2);
            Array.Resize(ref rgba, rgba.Length * 2);

            model.Xyz = xyz;
            model.Uv = uv;
            model.Rgba = rgba;
        }

        int vi = model.VerticesCount * 3;
        int ui = model.VerticesCount * 2;
        int ri = model.VerticesCount * 4;

        model.Xyz[vi] = x;
        model.Xyz[vi + 1] = y;
        model.Xyz[vi + 2] = z;

        model.Uv[ui] = u;
        model.Uv[ui + 1] = v;

        model.Rgba[ri] = (byte)Game.ColorR(color);
        model.Rgba[ri + 1] = (byte)Game.ColorG(color);
        model.Rgba[ri + 2] = (byte)Game.ColorB(color);
        model.Rgba[ri + 3] = (byte)Game.ColorA(color);

        model.VerticesCount++;
    }

    internal static void AddIndex(ModelData model, int index)
    {
        if (model.IndicesCount >= model.Indices.Length)
        {
            var indices = model.Indices;
            Array.Resize(ref indices, indices.Length * 2);
            model.Indices = indices;
        }
        model.Indices[model.IndicesCount++] = index;
    }
}

public enum DrawMode
{
    Triangles = 0,
    Lines = 1
}