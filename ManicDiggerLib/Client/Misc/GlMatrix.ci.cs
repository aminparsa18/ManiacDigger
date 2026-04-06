
using OpenTK.Mathematics;

/// 4x4 Matrix
public class Mat4
{
    /// Creates a new identity mat4
    /// Returns {mat4} a new 4x4 matrix
    public static float[] Create()
    {
        float[] output = new float[16];
        output[0] = 1;
        output[1] = 0;
        output[2] = 0;
        output[3] = 0;
        output[4] = 0;
        output[5] = 1;
        output[6] = 0;
        output[7] = 0;
        output[8] = 0;
        output[9] = 0;
        output[10] = 1;
        output[11] = 0;
        output[12] = 0;
        output[13] = 0;
        output[14] = 0;
        output[15] = 1;
        return output;
    }

    /// Creates a new mat4 initialized with values from an existing matrix
    /// Returns {mat4} a new 4x4 matrix
    public static float[] CloneIt(
        /// {mat4} a matrix to clone
        float[] a)
    {
        float[] output = new float[16];
        output[0] = a[0];
        output[1] = a[1];
        output[2] = a[2];
        output[3] = a[3];
        output[4] = a[4];
        output[5] = a[5];
        output[6] = a[6];
        output[7] = a[7];
        output[8] = a[8];
        output[9] = a[9];
        output[10] = a[10];
        output[11] = a[11];
        output[12] = a[12];
        output[13] = a[13];
        output[14] = a[14];
        output[15] = a[15];
        return output;
    }

    /// Copy the values from one mat4 to another
    /// Returns {mat4} out
    public static float[] Copy(
        /// {mat4} out the receiving matrix
        float[] output,
        /// {mat4} a the source matrix
        float[] a)
    {
        output[0] = a[0];
        output[1] = a[1];
        output[2] = a[2];
        output[3] = a[3];
        output[4] = a[4];
        output[5] = a[5];
        output[6] = a[6];
        output[7] = a[7];
        output[8] = a[8];
        output[9] = a[9];
        output[10] = a[10];
        output[11] = a[11];
        output[12] = a[12];
        output[13] = a[13];
        output[14] = a[14];
        output[15] = a[15];
        return output;
    }

    /// Set a mat4 to the identity matrix
    /// Returns {mat4} out
    public static float[] Identity_(
        /// {mat4} out the receiving matrix
        float[] output)
    {
        output[0] = 1;
        output[1] = 0;
        output[2] = 0;
        output[3] = 0;
        output[4] = 0;
        output[5] = 1;
        output[6] = 0;
        output[7] = 0;
        output[8] = 0;
        output[9] = 0;
        output[10] = 1;
        output[11] = 0;
        output[12] = 0;
        output[13] = 0;
        output[14] = 0;
        output[15] = 1;
        return output;
    }

    /// Transpose the values of a mat4
    /// @returns {mat4} out
    //mat4.transpose = function(output, a) {
    public static float[] Transpose(
        /// @param {mat4} out the receiving matrix
        float[] output,
        /// @param {mat4} a the source matrix
        float[] a)
    {
        // If we are transposing ourselves we can skip a few steps but have to cache some values
        if (output == a)
        {
            float a01 = a[1]; float a02 = a[2]; float a03 = a[3];
            float a12 = a[6]; float a13 = a[7];
            float a23 = a[11];

            output[1] = a[4];
            output[2] = a[8];
            output[3] = a[12];
            output[4] = a01;
            output[6] = a[9];
            output[7] = a[13];
            output[8] = a02;
            output[9] = a12;
            output[11] = a[14];
            output[12] = a03;
            output[13] = a13;
            output[14] = a23;
        }
        else
        {
            output[0] = a[0];
            output[1] = a[4];
            output[2] = a[8];
            output[3] = a[12];
            output[4] = a[1];
            output[5] = a[5];
            output[6] = a[9];
            output[7] = a[13];
            output[8] = a[2];
            output[9] = a[6];
            output[10] = a[10];
            output[11] = a[14];
            output[12] = a[3];
            output[13] = a[7];
            output[14] = a[11];
            output[15] = a[15];
        }

        return output;
    }

    /// Inverts a mat4
    /// @returns {mat4} out
    public static float[] Invert(
        /// {mat4} out the receiving matrix
        float[] output,
        /// {mat4} a the source matrix
        float[] a)
    {
        float a00 = a[0]; float a01 = a[1]; float a02 = a[2]; float a03 = a[3];
        float a10 = a[4]; float a11 = a[5]; float a12 = a[6]; float a13 = a[7];
        float a20 = a[8]; float a21 = a[9]; float a22 = a[10]; float a23 = a[11];
        float a30 = a[12]; float a31 = a[13]; float a32 = a[14]; float a33 = a[15];

        float b00 = a00 * a11 - a01 * a10;
        float b01 = a00 * a12 - a02 * a10;
        float b02 = a00 * a13 - a03 * a10;
        float b03 = a01 * a12 - a02 * a11;
        float b04 = a01 * a13 - a03 * a11;
        float b05 = a02 * a13 - a03 * a12;
        float b06 = a20 * a31 - a21 * a30;
        float b07 = a20 * a32 - a22 * a30;
        float b08 = a20 * a33 - a23 * a30;
        float b09 = a21 * a32 - a22 * a31;
        float b10 = a21 * a33 - a23 * a31;
        float b11 = a22 * a33 - a23 * a32;

        // Calculate the determinant
        float det = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;

        if (det == 0)
        {
            return null;
        }
        float one = 1;
        det = one / det;

        output[0] = (a11 * b11 - a12 * b10 + a13 * b09) * det;
        output[1] = (a02 * b10 - a01 * b11 - a03 * b09) * det;
        output[2] = (a31 * b05 - a32 * b04 + a33 * b03) * det;
        output[3] = (a22 * b04 - a21 * b05 - a23 * b03) * det;
        output[4] = (a12 * b08 - a10 * b11 - a13 * b07) * det;
        output[5] = (a00 * b11 - a02 * b08 + a03 * b07) * det;
        output[6] = (a32 * b02 - a30 * b05 - a33 * b01) * det;
        output[7] = (a20 * b05 - a22 * b02 + a23 * b01) * det;
        output[8] = (a10 * b10 - a11 * b08 + a13 * b06) * det;
        output[9] = (a01 * b08 - a00 * b10 - a03 * b06) * det;
        output[10] = (a30 * b04 - a31 * b02 + a33 * b00) * det;
        output[11] = (a21 * b02 - a20 * b04 - a23 * b00) * det;
        output[12] = (a11 * b07 - a10 * b09 - a12 * b06) * det;
        output[13] = (a00 * b09 - a01 * b07 + a02 * b06) * det;
        output[14] = (a31 * b01 - a30 * b03 - a32 * b00) * det;
        output[15] = (a20 * b03 - a21 * b01 + a22 * b00) * det;

        return output;
    }

    /// Calculates the adjugate of a mat4
    /// @returns {mat4} out
    public static float[] Adjoint(
        /// @param {mat4} out the receiving matrix
        float[] output,
        /// @param {mat4} a the source matrix
        float[] a)
    {
        float a00 = a[0]; float a01 = a[1]; float a02 = a[2]; float a03 = a[3];
        float a10 = a[4]; float a11 = a[5]; float a12 = a[6]; float a13 = a[7];
        float a20 = a[8]; float a21 = a[9]; float a22 = a[10]; float a23 = a[11];
        float a30 = a[12]; float a31 = a[13]; float a32 = a[14]; float a33 = a[15];

        output[0] = (a11 * (a22 * a33 - a23 * a32) - a21 * (a12 * a33 - a13 * a32) + a31 * (a12 * a23 - a13 * a22));
        output[1] = -(a01 * (a22 * a33 - a23 * a32) - a21 * (a02 * a33 - a03 * a32) + a31 * (a02 * a23 - a03 * a22));
        output[2] = (a01 * (a12 * a33 - a13 * a32) - a11 * (a02 * a33 - a03 * a32) + a31 * (a02 * a13 - a03 * a12));
        output[3] = -(a01 * (a12 * a23 - a13 * a22) - a11 * (a02 * a23 - a03 * a22) + a21 * (a02 * a13 - a03 * a12));
        output[4] = -(a10 * (a22 * a33 - a23 * a32) - a20 * (a12 * a33 - a13 * a32) + a30 * (a12 * a23 - a13 * a22));
        output[5] = (a00 * (a22 * a33 - a23 * a32) - a20 * (a02 * a33 - a03 * a32) + a30 * (a02 * a23 - a03 * a22));
        output[6] = -(a00 * (a12 * a33 - a13 * a32) - a10 * (a02 * a33 - a03 * a32) + a30 * (a02 * a13 - a03 * a12));
        output[7] = (a00 * (a12 * a23 - a13 * a22) - a10 * (a02 * a23 - a03 * a22) + a20 * (a02 * a13 - a03 * a12));
        output[8] = (a10 * (a21 * a33 - a23 * a31) - a20 * (a11 * a33 - a13 * a31) + a30 * (a11 * a23 - a13 * a21));
        output[9] = -(a00 * (a21 * a33 - a23 * a31) - a20 * (a01 * a33 - a03 * a31) + a30 * (a01 * a23 - a03 * a21));
        output[10] = (a00 * (a11 * a33 - a13 * a31) - a10 * (a01 * a33 - a03 * a31) + a30 * (a01 * a13 - a03 * a11));
        output[11] = -(a00 * (a11 * a23 - a13 * a21) - a10 * (a01 * a23 - a03 * a21) + a20 * (a01 * a13 - a03 * a11));
        output[12] = -(a10 * (a21 * a32 - a22 * a31) - a20 * (a11 * a32 - a12 * a31) + a30 * (a11 * a22 - a12 * a21));
        output[13] = (a00 * (a21 * a32 - a22 * a31) - a20 * (a01 * a32 - a02 * a31) + a30 * (a01 * a22 - a02 * a21));
        output[14] = -(a00 * (a11 * a32 - a12 * a31) - a10 * (a01 * a32 - a02 * a31) + a30 * (a01 * a12 - a02 * a11));
        output[15] = (a00 * (a11 * a22 - a12 * a21) - a10 * (a01 * a22 - a02 * a21) + a20 * (a01 * a12 - a02 * a11));
        return output;
    }

    /// Calculates the determinant of a mat4
    /// @returns {Number} determinant of a
    public static float Determinant(
        /// @param {mat4} a the source matrix
        float[] a)
    {
        float a00 = a[0]; float a01 = a[1]; float a02 = a[2]; float a03 = a[3];
        float a10 = a[4]; float a11 = a[5]; float a12 = a[6]; float a13 = a[7];
        float a20 = a[8]; float a21 = a[9]; float a22 = a[10]; float a23 = a[11];
        float a30 = a[12]; float a31 = a[13]; float a32 = a[14]; float a33 = a[15];

        float b00 = a00 * a11 - a01 * a10;
        float b01 = a00 * a12 - a02 * a10;
        float b02 = a00 * a13 - a03 * a10;
        float b03 = a01 * a12 - a02 * a11;
        float b04 = a01 * a13 - a03 * a11;
        float b05 = a02 * a13 - a03 * a12;
        float b06 = a20 * a31 - a21 * a30;
        float b07 = a20 * a32 - a22 * a30;
        float b08 = a20 * a33 - a23 * a30;
        float b09 = a21 * a32 - a22 * a31;
        float b10 = a21 * a33 - a23 * a31;
        float b11 = a22 * a33 - a23 * a32;

        // Calculate the determinant
        return b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;
    }

    /// Multiplies two mat4's
    /// @returns {mat4} out
    public static float[] Multiply(
        /// @param {mat4} out the receiving matrix
        float[] output,
        /// @param {mat4} a the first operand
        float[] a,
        /// @param {mat4} b the second operand
        float[] b)
    {
        float a00 = a[0]; float a01 = a[1]; float a02 = a[2]; float a03 = a[3];
        float a10 = a[4]; float a11 = a[5]; float a12 = a[6]; float a13 = a[7];
        float a20 = a[8]; float a21 = a[9]; float a22 = a[10]; float a23 = a[11];
        float a30 = a[12]; float a31 = a[13]; float a32 = a[14]; float a33 = a[15];

        // Cache only the current line of the second matrix
        float b0 = b[0]; float b1 = b[1]; float b2 = b[2]; float b3 = b[3];
        output[0] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
        output[1] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
        output[2] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
        output[3] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

        b0 = b[4]; b1 = b[5]; b2 = b[6]; b3 = b[7];
        output[4] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
        output[5] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
        output[6] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
        output[7] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

        b0 = b[8]; b1 = b[9]; b2 = b[10]; b3 = b[11];
        output[8] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
        output[9] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
        output[10] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
        output[11] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;

        b0 = b[12]; b1 = b[13]; b2 = b[14]; b3 = b[15];
        output[12] = b0 * a00 + b1 * a10 + b2 * a20 + b3 * a30;
        output[13] = b0 * a01 + b1 * a11 + b2 * a21 + b3 * a31;
        output[14] = b0 * a02 + b1 * a12 + b2 * a22 + b3 * a32;
        output[15] = b0 * a03 + b1 * a13 + b2 * a23 + b3 * a33;
        return output;
    }

    /// Alias for {@link mat4.multiply}
    public static float[] Mul(float[] output, float[] a, float[] b)
    {
        return Multiply(output, a, b);
    }

    /// Translate a mat4 by the given vector
    /// @returns {mat4} out
    public static float[] Translate(
        /// {mat4} out the receiving matrix
        float[] output,
        /// {mat4} a the matrix to translate
        float[] a,
        /// {vec3} v vector to translate by
        Vector3 v)
    {
        float x = v[0]; float y = v[1]; float z = v[2];
        float a00; float a01; float a02; float a03;
        float a10; float a11; float a12; float a13;
        float a20; float a21; float a22; float a23;

        if (a == output)
        {
            output[12] = a[0] * x + a[4] * y + a[8] * z + a[12];
            output[13] = a[1] * x + a[5] * y + a[9] * z + a[13];
            output[14] = a[2] * x + a[6] * y + a[10] * z + a[14];
            output[15] = a[3] * x + a[7] * y + a[11] * z + a[15];
        }
        else
        {
            a00 = a[0]; a01 = a[1]; a02 = a[2]; a03 = a[3];
            a10 = a[4]; a11 = a[5]; a12 = a[6]; a13 = a[7];
            a20 = a[8]; a21 = a[9]; a22 = a[10]; a23 = a[11];

            output[0] = a00; output[1] = a01; output[2] = a02; output[3] = a03;
            output[4] = a10; output[5] = a11; output[6] = a12; output[7] = a13;
            output[8] = a20; output[9] = a21; output[10] = a22; output[11] = a23;

            output[12] = a00 * x + a10 * y + a20 * z + a[12];
            output[13] = a01 * x + a11 * y + a21 * z + a[13];
            output[14] = a02 * x + a12 * y + a22 * z + a[14];
            output[15] = a03 * x + a13 * y + a23 * z + a[15];
        }

        return output;
    }

    /// Scales the mat4 by the dimensions in the given vec3
    /// @returns {mat4} out
    public static float[] Scale(
        /// {mat4} out the receiving matrix
        float[] output,
        /// {mat4} a the matrix to scale
        float[] a,
        /// {vec3} v the vec3 to scale the matrix by
        Vector3 v)
    {
        float x = v[0]; float y = v[1]; float z = v[2];

        output[0] = a[0] * x;
        output[1] = a[1] * x;
        output[2] = a[2] * x;
        output[3] = a[3] * x;
        output[4] = a[4] * y;
        output[5] = a[5] * y;
        output[6] = a[6] * y;
        output[7] = a[7] * y;
        output[8] = a[8] * z;
        output[9] = a[9] * z;
        output[10] = a[10] * z;
        output[11] = a[11] * z;
        output[12] = a[12];
        output[13] = a[13];
        output[14] = a[14];
        output[15] = a[15];
        return output;
    }

    /// Rotates a mat4 by the given angle
    /// @returns {mat4} out
    public static float[] Rotate(
        /// {mat4} out the receiving matrix
        float[] output,
        /// {mat4} a the matrix to rotate
        float[] a,
        /// {Number} rad the angle to rotate the matrix by
        float rad,
        /// {vec3} axis the axis to rotate around
        Vector3 axis)
    {
        float x = axis[0]; float y = axis[1]; float z = axis[2];
        float len = (float)MathHelper.Sqrt(x * x + y * y + z * z);
        float s; float c; float t;
        float a00; float a01; float a02; float a03;
        float a10; float a11; float a12; float a13;
        float a20; float a21; float a22; float a23;
        float b00; float b01; float b02;
        float b10; float b11; float b12;
        float b20; float b21; float b22;

        if (MathHelper.Abs(len) < GLMAT_EPSILON) { return null; }

        len = 1 / len;
        x *= len;
        y *= len;
        z *= len;

        s = (float)MathHelper.Sin(rad);
        c = (float)MathHelper.Cos(rad);
        t = 1 - c;

        a00 = a[0]; a01 = a[1]; a02 = a[2]; a03 = a[3];
        a10 = a[4]; a11 = a[5]; a12 = a[6]; a13 = a[7];
        a20 = a[8]; a21 = a[9]; a22 = a[10]; a23 = a[11];

        // Construct the elements of the rotation matrix
        b00 = x * x * t + c; b01 = y * x * t + z * s; b02 = z * x * t - y * s;
        b10 = x * y * t - z * s; b11 = y * y * t + c; b12 = z * y * t + x * s;
        b20 = x * z * t + y * s; b21 = y * z * t - x * s; b22 = z * z * t + c;

        // Perform rotation-specific matrix multiplication
        output[0] = a00 * b00 + a10 * b01 + a20 * b02;
        output[1] = a01 * b00 + a11 * b01 + a21 * b02;
        output[2] = a02 * b00 + a12 * b01 + a22 * b02;
        output[3] = a03 * b00 + a13 * b01 + a23 * b02;
        output[4] = a00 * b10 + a10 * b11 + a20 * b12;
        output[5] = a01 * b10 + a11 * b11 + a21 * b12;
        output[6] = a02 * b10 + a12 * b11 + a22 * b12;
        output[7] = a03 * b10 + a13 * b11 + a23 * b12;
        output[8] = a00 * b20 + a10 * b21 + a20 * b22;
        output[9] = a01 * b20 + a11 * b21 + a21 * b22;
        output[10] = a02 * b20 + a12 * b21 + a22 * b22;
        output[11] = a03 * b20 + a13 * b21 + a23 * b22;

        if (a != output)
        {
            // If the source and destination differ, copy the unchanged last row
            output[12] = a[12];
            output[13] = a[13];
            output[14] = a[14];
            output[15] = a[15];
        }
        return output;
    }

    /// Rotates a matrix by the given angle around the X axis
    /// @returns {mat4} out
    public static float[] RotateX(
        /// {mat4} out the receiving matrix
        float[] output,
        /// {mat4} a the matrix to rotate
        float[] a,
        /// {Number} rad the angle to rotate the matrix by
        float rad)
    {
        float s = (float)MathHelper.Sin(rad);
        float c = (float)MathHelper.Cos(rad);
        float a10 = a[4];
        float a11 = a[5];
        float a12 = a[6];
        float a13 = a[7];
        float a20 = a[8];
        float a21 = a[9];
        float a22 = a[10];
        float a23 = a[11];

        if (a != output)
        {
            // If the source and destination differ, copy the unchanged rows
            output[0] = a[0];
            output[1] = a[1];
            output[2] = a[2];
            output[3] = a[3];
            output[12] = a[12];
            output[13] = a[13];
            output[14] = a[14];
            output[15] = a[15];
        }

        // Perform axis-specific matrix multiplication
        output[4] = a10 * c + a20 * s;
        output[5] = a11 * c + a21 * s;
        output[6] = a12 * c + a22 * s;
        output[7] = a13 * c + a23 * s;
        output[8] = a20 * c - a10 * s;
        output[9] = a21 * c - a11 * s;
        output[10] = a22 * c - a12 * s;
        output[11] = a23 * c - a13 * s;
        return output;
    }

    /// Rotates a matrix by the given angle around the Y axis
    /// @returns {mat4} out
    public static float[] RotateY(
        /// {mat4} out the receiving matrix
        float[] output,
        /// {mat4} a the matrix to rotate
        float[] a,
        /// {Number} rad the angle to rotate the matrix by
        float rad)
    {
        float s = (float)MathHelper.Sin(rad);
        float c = (float)MathHelper.Cos(rad);
        float a00 = a[0];
        float a01 = a[1];
        float a02 = a[2];
        float a03 = a[3];
        float a20 = a[8];
        float a21 = a[9];
        float a22 = a[10];
        float a23 = a[11];

        if (a != output)
        {
            // If the source and destination differ, copy the unchanged rows
            output[4] = a[4];
            output[5] = a[5];
            output[6] = a[6];
            output[7] = a[7];
            output[12] = a[12];
            output[13] = a[13];
            output[14] = a[14];
            output[15] = a[15];
        }

        // Perform axis-specific matrix multiplication
        output[0] = a00 * c - a20 * s;
        output[1] = a01 * c - a21 * s;
        output[2] = a02 * c - a22 * s;
        output[3] = a03 * c - a23 * s;
        output[8] = a00 * s + a20 * c;
        output[9] = a01 * s + a21 * c;
        output[10] = a02 * s + a22 * c;
        output[11] = a03 * s + a23 * c;
        return output;
    }

    /// Rotates a matrix by the given angle around the Z axis
    /// @returns {mat4} out
    public static float[] RotateZ(
        /// {mat4} out the receiving matrix
        float[] output,
        /// {mat4} a the matrix to rotate
        float[] a,
        /// {Number} rad the angle to rotate the matrix by
        float rad)
    {
        float s = (float)MathHelper.Sin(rad);
        float c = (float)MathHelper.Cos(rad);
        float a00 = a[0];
        float a01 = a[1];
        float a02 = a[2];
        float a03 = a[3];
        float a10 = a[4];
        float a11 = a[5];
        float a12 = a[6];
        float a13 = a[7];

        if (a != output)
        {
            // If the source and destination differ, copy the unchanged last row
            output[8] = a[8];
            output[9] = a[9];
            output[10] = a[10];
            output[11] = a[11];
            output[12] = a[12];
            output[13] = a[13];
            output[14] = a[14];
            output[15] = a[15];
        }

        // Perform axis-specific matrix multiplication
        output[0] = a00 * c + a10 * s;
        output[1] = a01 * c + a11 * s;
        output[2] = a02 * c + a12 * s;
        output[3] = a03 * c + a13 * s;
        output[4] = a10 * c - a00 * s;
        output[5] = a11 * c - a01 * s;
        output[6] = a12 * c - a02 * s;
        output[7] = a13 * c - a03 * s;
        return output;
    }

    /// Creates a matrix from a quaternion rotation and vector translation
    /// This is equivalent to (but much faster than):
    ///     mat4.identity(dest);
    ///     mat4.translate(dest, vec);
    ///     var quatMat = mat4.create();
    ///     quat4.toMat4(quat, quatMat);
    ///     mat4.multiply(dest, quatMat);
    /// @returns {mat4} out
    public static float[] FromRotationTranslation(
        /// {mat4} out mat4 receiving operation result
        float[] output,
        /// {quat4} q Rotation quaternion
        float[] q,
        /// {vec3} v Translation vector
        float[] v)
    {
        // Quaternion math
        float x = q[0]; float y = q[1]; float z = q[2]; float w = q[3];
        float x2 = x + x;
        float y2 = y + y;
        float z2 = z + z;

        float xx = x * x2;
        float xy = x * y2;
        float xz = x * z2;
        float yy = y * y2;
        float yz = y * z2;
        float zz = z * z2;
        float wx = w * x2;
        float wy = w * y2;
        float wz = w * z2;

        output[0] = 1 - (yy + zz);
        output[1] = xy + wz;
        output[2] = xz - wy;
        output[3] = 0;
        output[4] = xy - wz;
        output[5] = 1 - (xx + zz);
        output[6] = yz + wx;
        output[7] = 0;
        output[8] = xz + wy;
        output[9] = yz - wx;
        output[10] = 1 - (xx + yy);
        output[11] = 0;
        output[12] = v[0];
        output[13] = v[1];
        output[14] = v[2];
        output[15] = 1;

        return output;
    }

    /// Calculates a 4x4 matrix from the given quaternion
    /// @returns {mat4} out
    public static float[] FromQuat(
        /// {mat4} out mat4 receiving operation result
        float[] output,
        /// {quat} q Quaternion to create matrix from
        float[] q)
    {
        float x = q[0]; float y = q[1]; float z = q[2]; float w = q[3];
        float x2 = x + x;
        float y2 = y + y;
        float z2 = z + z;

        float xx = x * x2;
        float xy = x * y2;
        float xz = x * z2;
        float yy = y * y2;
        float yz = y * z2;
        float zz = z * z2;
        float wx = w * x2;
        float wy = w * y2;
        float wz = w * z2;

        output[0] = 1 - (yy + zz);
        output[1] = xy + wz;
        output[2] = xz - wy;
        output[3] = 0;

        output[4] = xy - wz;
        output[5] = 1 - (xx + zz);
        output[6] = yz + wx;
        output[7] = 0;

        output[8] = xz + wy;
        output[9] = yz - wx;
        output[10] = 1 - (xx + yy);
        output[11] = 0;

        output[12] = 0;
        output[13] = 0;
        output[14] = 0;
        output[15] = 1;

        return output;
    }

    /// Generates a frustum matrix with the given bounds
    /// @returns {mat4} out
    public static float[] Frustum(
        /// {mat4} out mat4 frustum matrix will be written into
        float[] output,
        /// {Number} left Left bound of the frustum
        float left,
        /// {Number} right Right bound of the frustum
        float right,
        /// {Number} bottom Bottom bound of the frustum
        float bottom,
        /// {Number} top Top bound of the frustum
        float top,
        /// {Number} near Near bound of the frustum
        float near,
        /// {Number} far Far bound of the frustum
        float far)
    {
        float rl = 1 / (right - left);
        float tb = 1 / (top - bottom);
        float nf = 1 / (near - far);
        output[0] = (near * 2) * rl;
        output[1] = 0;
        output[2] = 0;
        output[3] = 0;
        output[4] = 0;
        output[5] = (near * 2) * tb;
        output[6] = 0;
        output[7] = 0;
        output[8] = (right + left) * rl;
        output[9] = (top + bottom) * tb;
        output[10] = (far + near) * nf;
        output[11] = -1;
        output[12] = 0;
        output[13] = 0;
        output[14] = (far * near * 2) * nf;
        output[15] = 0;
        return output;
    }

    /// Generates a perspective projection matrix with the given bounds
    /// @returns {mat4} out
    public static float[] Perspective(
        /// {mat4} out mat4 frustum matrix will be written into
        float[] output,
        /// {number} fovy Vertical field of view in radians
        float fovy,
        /// {number} aspect Aspect ratio. typically viewport width/height
        float aspect,
        /// {number} near Near bound of the frustum
        float near,
        /// {number} far Far bound of the frustum
        float far)
    {
        float one = 1;
        float f = one / (float)MathHelper.Tan(fovy / 2);
        float nf = 1 / (near - far);
        output[0] = f / aspect;
        output[1] = 0;
        output[2] = 0;
        output[3] = 0;
        output[4] = 0;
        output[5] = f;
        output[6] = 0;
        output[7] = 0;
        output[8] = 0;
        output[9] = 0;
        output[10] = (far + near) * nf;
        output[11] = -1;
        output[12] = 0;
        output[13] = 0;
        output[14] = (2 * far * near) * nf;
        output[15] = 0;
        return output;
    }

    /// Generates a orthogonal projection matrix with the given bounds
    /// @returns {mat4} out
    public static float[] Ortho(
        /// {mat4} out mat4 frustum matrix will be written into
        float[] output,
        /// {number} left Left bound of the frustum
        float left,
        /// {number} right Right bound of the frustum
        float right,
        /// {number} bottom Bottom bound of the frustum
        float bottom,
        /// {number} top Top bound of the frustum
        float top,
        /// {number} near Near bound of the frustum
        float near,
        /// {number} far Far bound of the frustum
        float far)
    {
        float lr = 1 / (left - right);
        float bt = 1 / (bottom - top);
        float nf = 1 / (near - far);
        output[0] = -2 * lr;
        output[1] = 0;
        output[2] = 0;
        output[3] = 0;
        output[4] = 0;
        output[5] = -2 * bt;
        output[6] = 0;
        output[7] = 0;
        output[8] = 0;
        output[9] = 0;
        output[10] = 2 * nf;
        output[11] = 0;
        output[12] = (left + right) * lr;
        output[13] = (top + bottom) * bt;
        output[14] = (far + near) * nf;
        output[15] = 1;
        return output;
    }

    private const float GLMAT_EPSILON = 1e-6f;

    /// Generates a look-at matrix with the given eye position, focal point, and up axis
    /// @returns {mat4} out
    public static float[] LookAt(
        /// {mat4} out mat4 frustum matrix will be written into
        float[] output,
        /// {vec3} eye Position of the viewer
        Vector3 eye,
        /// {vec3} center Point the viewer is looking at
        Vector3 center,
        /// {vec3} up vec3 pointing up
        Vector3 up)
    {
        float x0; float x1; float x2; float y0; float y1; float y2; float z0; float z1; float z2; float len;
        float eyex = eye[0];
        float eyey = eye[1];
        float eyez = eye[2];
        float upx = up[0];
        float upy = up[1];
        float upz = up[2];
        float centerx = center[0];
        float centery = center[1];
        float centerz = center[2];

        if (MathHelper.Abs(eyex - centerx) < GLMAT_EPSILON &&
            MathHelper.Abs(eyey - centery) < GLMAT_EPSILON &&
            MathHelper.Abs(eyez - centerz) < GLMAT_EPSILON)
        {
            return Identity_(output);
        }

        z0 = eyex - centerx;
        z1 = eyey - centery;
        z2 = eyez - centerz;

        len = 1 / (float)MathHelper.Sqrt(z0 * z0 + z1 * z1 + z2 * z2);
        z0 *= len;
        z1 *= len;
        z2 *= len;

        x0 = upy * z2 - upz * z1;
        x1 = upz * z0 - upx * z2;
        x2 = upx * z1 - upy * z0;
        len = (float)MathHelper.Sqrt(x0 * x0 + x1 * x1 + x2 * x2);
        if (len == 0)
        {
            x0 = 0;
            x1 = 0;
            x2 = 0;
        }
        else
        {
            len = 1 / len;
            x0 *= len;
            x1 *= len;
            x2 *= len;
        }

        y0 = z1 * x2 - z2 * x1;
        y1 = z2 * x0 - z0 * x2;
        y2 = z0 * x1 - z1 * x0;

        len = (float)MathHelper.Sqrt(y0 * y0 + y1 * y1 + y2 * y2);
        if (len == 0)
        {
            y0 = 0;
            y1 = 0;
            y2 = 0;
        }
        else
        {
            len = 1 / len;
            y0 *= len;
            y1 *= len;
            y2 *= len;
        }

        output[0] = x0;
        output[1] = y0;
        output[2] = z0;
        output[3] = 0;
        output[4] = x1;
        output[5] = y1;
        output[6] = z1;
        output[7] = 0;
        output[8] = x2;
        output[9] = y2;
        output[10] = z2;
        output[11] = 0;
        output[12] = -(x0 * eyex + x1 * eyey + x2 * eyez);
        output[13] = -(y0 * eyex + y1 * eyey + y2 * eyez);
        output[14] = -(z0 * eyex + z1 * eyey + z2 * eyez);
        output[15] = 1;

        return output;
    }

    ///**
    // * Returns a string representation of a mat4
    // *
    // * @param {mat4} mat matrix to represent as a string
    // * @returns {String} string representation of the matrix
    // */
    //mat4.str = function (a) {
    //    return 'mat4(' + a[0] + ', ' + a[1] + ', ' + a[2] + ', ' + a[3] + ', ' +
    //                    a[4] + ', ' + a[5] + ', ' + a[6] + ', ' + a[7] + ', ' +
    //                    a[8] + ', ' + a[9] + ', ' + a[10] + ', ' + a[11] + ', ' + 
    //                    a[12] + ', ' + a[13] + ', ' + a[14] + ', ' + a[15] + ')';
    //};

    //if(typeof(exports) !== 'undefined') {
    //    exports.mat4 = mat4;
    //}

    private static void f()
    {
    }
}