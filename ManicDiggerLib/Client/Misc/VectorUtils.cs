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

        output.X = (dx * MathF.Cos(yRot) + dy * MathF.Sin(xRot) * MathF.Sin(yRot) - dz * MathF.Cos(xRot) * MathF.Sin(yRot));
        output.Y = (dy * MathF.Cos(xRot) + dz * MathF.Sin(xRot));
        output.Z = (dx * MathF.Sin(yRot) - dy * MathF.Sin(xRot) * MathF.Cos(yRot) + dz * MathF.Cos(xRot) * MathF.Cos(yRot));
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
}
