using OpenTK.Mathematics;

/// <summary>
/// Performs frustum culling by extracting the 6 view frustum planes from
/// the current modelview and projection matrices, then testing objects against them.
/// Based on the article "Fast Extraction of Viewing Frustum Planes from the World-View-Projection Matrix (24-Jan-2001!)"
/// </summary>
public class FrustumCulling
{
    // Plane indices for clarity when accessing the frustum array.
    private const int Right = 0;
    private const int Left = 1;
    private const int Bottom = 2;
    private const int Top = 3;
    private const int Far = 4;
    private const int Near = 5;

    /// <summary>Platform utilities for math operations such as square root.</summary>
    internal GamePlatform platform;

    /// <summary>Provides the current modelview and projection matrices from the camera.</summary>
    internal ICameraMatrixProvider d_GetCameraMatrix;

    /// <summary>
    /// The 6 normalized frustum plane equations (A, B, C, D) where XYZ is the
    /// plane normal and W is the plane distance. Ordered: right, left, bottom, top, far, near.
    /// </summary>
    private readonly Vector4[] frustumPlanes = new Vector4[6];

    /// <summary>
    /// Tests whether a sphere is at least partially inside the view frustum.
    /// </summary>
    /// <param name="x">World-space X coordinate of the sphere center.</param>
    /// <param name="y">World-space Y coordinate of the sphere center.</param>
    /// <param name="z">World-space Z coordinate of the sphere center.</param>
    /// <param name="radius">Radius of the sphere.</param>
    /// <returns>
    /// <c>true</c> if the sphere intersects or is inside the frustum;
    /// <c>false</c> if it is fully outside any frustum plane.
    /// </returns>
    public bool SphereInFrustum(float x, float y, float z, float radius)
    {
        for (int i = 0; i < frustumPlanes.Length; i++)
        {
            float d = frustumPlanes[i].X * x
                    + frustumPlanes[i].Y * y
                    + frustumPlanes[i].Z * z
                    + frustumPlanes[i].W;
            if (d <= -radius) { return false; }
        }
        return true;
    }

    /// <summary>
    /// Recalculates the 6 frustum plane equations from the current modelview
    /// and projection matrices. Must be called once per frame before any
    /// <see cref="SphereInFrustum"/> calls.
    /// </summary>
    /// <remarks>
    /// Extracts plane equations of the form Ax+By+Cz+D=0 where (A,B,C) is the
    /// plane normal. Each plane is normalized so that D represents the true
    /// signed distance from the origin.
    /// </remarks>
    public void CalcFrustumEquations()
    {
        Matrix4 matModelView = d_GetCameraMatrix.GetModelViewMatrix();
        Matrix4 matProjection = d_GetCameraMatrix.GetProjectionMatrix();
        Matrix4.Mult(in matModelView, in matProjection, out Matrix4 m);

        frustumPlanes[Right] = NormalizePlane(m.Row0.W - m.Row0.X, m.Row1.W - m.Row1.X, m.Row2.W - m.Row2.X, m.Row3.W - m.Row3.X);
        frustumPlanes[Left] = NormalizePlane(m.Row0.W + m.Row0.X, m.Row1.W + m.Row1.X, m.Row2.W + m.Row2.X, m.Row3.W + m.Row3.X);
        frustumPlanes[Bottom] = NormalizePlane(m.Row0.W + m.Row0.Y, m.Row1.W + m.Row1.Y, m.Row2.W + m.Row2.Y, m.Row3.W + m.Row3.Y);
        frustumPlanes[Top] = NormalizePlane(m.Row0.W - m.Row0.Y, m.Row1.W - m.Row1.Y, m.Row2.W - m.Row2.Y, m.Row3.W - m.Row3.Y);
        frustumPlanes[Far] = NormalizePlane(m.Row0.W - m.Row0.Z, m.Row1.W - m.Row1.Z, m.Row2.W - m.Row2.Z, m.Row3.W - m.Row3.Z);
        frustumPlanes[Near] = NormalizePlane(m.Row0.W + m.Row0.Z, m.Row1.W + m.Row1.Z, m.Row2.W + m.Row2.Z, m.Row3.W + m.Row3.Z);
    }

    /// <summary>
    /// Normalizes a plane equation (A, B, C, D) by dividing all components
    /// by the magnitude of the normal (A, B, C), so that D represents the
    /// true signed distance from the origin to the plane.
    /// </summary>
    private Vector4 NormalizePlane(float a, float b, float c, float d)
    {
        float magnitude = platform.MathSqrt(a * a + b * b + c * c);
        return new Vector4(a / magnitude, b / magnitude, c / magnitude, d / magnitude);
    }
}

/// <summary>
/// Provides the current modelview and projection matrices from the camera,
/// used by <see cref="FrustumCulling"/> to extract frustum planes.
/// </summary>
public interface ICameraMatrixProvider
{
    /// <summary>Returns the current modelview matrix.</summary>
    Matrix4 GetModelViewMatrix();

    /// <summary>Returns the current projection matrix.</summary>
    Matrix4 GetProjectionMatrix();
}