public class InterpolatedObject
{
}
public abstract class IInterpolation
{
    public abstract InterpolatedObject Interpolate(InterpolatedObject a, InterpolatedObject b, float progress);
}
public abstract class INetworkInterpolation
{
    public abstract void AddNetworkPacket(InterpolatedObject c, int timeMilliseconds);
    public abstract InterpolatedObject InterpolatedState(int timeMilliseconds);
}
public class Packet_
{
    internal int timestampMilliseconds;
    internal InterpolatedObject content;
}
public class NetworkInterpolation : INetworkInterpolation
{
    public NetworkInterpolation()
    {
        received = new Packet_[128];
        DELAYMILLISECONDS = 200;
        EXTRAPOLATION_TIMEMILLISECONDS = 200;
    }
    internal IInterpolation req;
    internal bool EXTRAPOLATE;
    internal int DELAYMILLISECONDS;
    internal int EXTRAPOLATION_TIMEMILLISECONDS;
    private readonly Packet_[] received;
    private int receivedCount;
    public override void AddNetworkPacket(InterpolatedObject c, int timeMilliseconds)
    {
        Packet_ p = new()
        {
            content = c,
            timestampMilliseconds = timeMilliseconds
        };

        int max = 100;
        if (receivedCount >= max)
        {
            for (int i = 0; i < max - 1; i++)
            {
                received[i] = received[i + 1];
            }
            receivedCount = max - 1;
        }

        received[receivedCount++] = p;
    }
    public override InterpolatedObject InterpolatedState(int timeMilliseconds)
    {
        int curtimeMilliseconds = timeMilliseconds;
        int interpolationtimeMilliseconds = curtimeMilliseconds - DELAYMILLISECONDS;
        int p1;
        int p2;
        if (receivedCount == 0)
        {
            return null;
        }
        InterpolatedObject result;
        if (receivedCount > 0 && interpolationtimeMilliseconds < received[0].timestampMilliseconds)
        {
            p1 = 0;
            p2 = 0;
        }
        //extrapolate
        else if (EXTRAPOLATE && (receivedCount >= 2)
            && interpolationtimeMilliseconds > received[receivedCount - 1].timestampMilliseconds)
        {
            p1 = receivedCount - 2;
            p2 = receivedCount - 1;
            interpolationtimeMilliseconds = Math.Min(interpolationtimeMilliseconds, received[receivedCount - 1].timestampMilliseconds + EXTRAPOLATION_TIMEMILLISECONDS);
        }
        else
        {
            p1 = 0;
            for (int i = 0; i < receivedCount; i++)
            {
                if (received[i].timestampMilliseconds <= interpolationtimeMilliseconds)
                {
                    p1 = i;
                }
            }
            p2 = p1;
            if (receivedCount - 1 > p1)
            {
                p2++;
            }
        }
        if (p1 == p2)
        {
            result = received[p1].content;
        }
        else
        {
            float one = 1;
            result = req.Interpolate(received[p1].content, received[p2].content,
                one * (interpolationtimeMilliseconds - received[p1].timestampMilliseconds)
                / (received[p2].timestampMilliseconds - received[p1].timestampMilliseconds));
        }
        return result;
    }
}

/// <summary>
/// Provides shortest-path angle interpolation for both 256-step and 360-degree representations.
/// </summary>
public class AngleInterpolation
{
    private const int CircleHalf256 = 128;
    private const int CircleFull256 = 256;
    private const int CircleHalf360 = 180;
    private const int CircleFull360 = 360;

    // Used to bias angles into a positive range before modulo.
    // Chosen as short.MaxValue (32767) — large enough to avoid negative results.
    private const int BiasValue = short.MaxValue;

    /// <summary>
    /// Interpolates between two angles in 256-step (byte) space via the shortest arc.
    /// </summary>
    /// <param name="a">Start angle (0–255).</param>
    /// <param name="b">End angle (0–255).</param>
    /// <param name="progress">Interpolation factor (0.0 = a, 1.0 = b).</param>
    /// <returns>Interpolated angle normalized to 0–255.</returns>
    public static int InterpolateAngle256(int a, int b, float progress)
    {
        if (progress != 0 && b != a)
        {
            int diff = NormalizeAngle256(b - a);
            if (diff >= CircleHalf256)
                diff -= CircleFull256;
            a += (int)(progress * diff);
        }
        return NormalizeAngle256(a);
    }

    /// <summary>
    /// Interpolates between two angles in degrees via the shortest arc.
    /// </summary>
    /// <param name="a">Start angle in degrees.</param>
    /// <param name="b">End angle in degrees.</param>
    /// <param name="progress">Interpolation factor (0.0 = a, 1.0 = b).</param>
    /// <returns>Interpolated angle normalized to 0–360.</returns>
    public static float InterpolateAngle360(float a, float b, float progress)
    {
        if (progress != 0 && b != a)
        {
            float diff = NormalizeAngle360(b - a);
            if (diff >= CircleHalf360)
                diff -= CircleFull360;
            a += progress * diff;
        }
        return NormalizeAngle360(a);
    }

    /// <summary>Normalizes an angle to the range 0–255.</summary>
    private static int NormalizeAngle256(int v)
    {
        return (v + BiasValue / 2) % CircleFull256;
    }

    /// <summary>Normalizes an angle to the range 0–360.</summary>
    private static float NormalizeAngle360(float v)
    {
        return (v + BiasValue / 2 / CircleFull360 * CircleFull360) % CircleFull360;
    }
}