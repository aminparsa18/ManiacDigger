using OpenTK.Mathematics;

public class VectorUtils
{
    public static void ToVectorInFixedSystem(float dx, float dy, float dz, float orientationx, float orientationy, ref Vector3 output)
    {
        if (dx == 0 && dy == 0 && dz == 0)
        {
            output = Vector3.Zero;
            return;
        }

        float xRot = orientationx;
        float yRot = orientationy;

        output.X = dx * MathF.Cos(yRot) + dy * MathF.Sin(xRot) * MathF.Sin(yRot) - dz * MathF.Cos(xRot) * MathF.Sin(yRot);
        output.Y = dy * MathF.Cos(xRot) + dz * MathF.Sin(xRot);
        output.Z = dx * MathF.Sin(yRot) - dy * MathF.Sin(xRot) * MathF.Cos(yRot) + dz * MathF.Cos(xRot) * MathF.Cos(yRot);
    }

    public static bool UnProject(int winX, int winY, int winZ, Matrix4 model, Matrix4 proj, int[] view, out Vector3 objPos)
    {
        objPos = Vector3.Zero;

        Matrix4.Mult(in model, in proj, out Matrix4 finalMatrix);
        finalMatrix.Invert();

        Vector4 inp;
        inp.X = winX;
        inp.Y = winY;
        inp.Z = winZ;
        inp.W = 1;

        // Map x and y from window coordinates
        inp.X = (inp.X - view[0]) / view[2];
        inp.Y = (inp.Y - view[1]) / view[3];

        // Map to range -1 to 1
        inp.X = inp.X * 2 - 1;
        inp.Y = inp.Y * 2 - 1;
        inp.Z = inp.Z * 2 - 1;

        Vector4.TransformRow(in inp, in finalMatrix, out Vector4 out_);

        if (out_.W == 0)
        {
            return false;
        }

        objPos.X = out_.X / out_.W;
        objPos.Y = out_.Y / out_.W;
        objPos.Z = out_.Z / out_.W;

        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the point (<paramref name="x"/>, <paramref name="y"/>)
    /// lies within the rectangle defined by origin (<paramref name="rx"/>, <paramref name="ry"/>)
    /// and size (<paramref name="rw"/>, <paramref name="rh"/>).
    /// </summary>
    public static bool PointInRect(float x, float y, float rx, float ry, float rw, float rh)
       => new Box2(rx, ry, rx + rw, ry + rh).ContainsExclusive(new Vector2(x, y));


    /// <summary>
    /// Returns the UV rectangle for a texture within a packed atlas grid.
    /// </summary>
    /// <param name="textureId">Flat index of the texture in the atlas.</param>
    /// <param name="texturesPacked">Number of textures along one axis of the atlas (assumed square).</param>
    /// <returns>A <see cref="RectangleF"/> in normalised [0,1] UV space.</returns>
    public static RectangleF GetAtlasRect(int textureId, int texturesPacked)
    {
        RectangleF r = new()
        {
            Y = 1f / texturesPacked * (textureId / texturesPacked),
            X = 1f / texturesPacked * (textureId % texturesPacked),
            Width = 1f / texturesPacked,
            Height = 1f / texturesPacked
        };
        return r;
    }
}
