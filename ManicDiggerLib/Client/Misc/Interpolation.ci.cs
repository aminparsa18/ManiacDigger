/// <summary>Marker base for objects that can be interpolated between two states.</summary>
public class InterpolatedObject { }

/// <summary>Stateless interpolation strategy between two <see cref="InterpolatedObject"/> snapshots.</summary>
public abstract class IInterpolation
{
    /// <summary>
    /// Returns the state between <paramref name="a"/> and <paramref name="b"/>
    /// at the given <paramref name="progress"/> (0 = a, 1 = b).
    /// </summary>
    public abstract InterpolatedObject Interpolate(InterpolatedObject a, InterpolatedObject b, float progress);
}

/// <summary>Timestamped network interpolation interface.</summary>
public abstract class INetworkInterpolation
{
    /// <summary>Records a received state snapshot with its server timestamp.</summary>
    public abstract void AddNetworkPacket(InterpolatedObject c, int timeMilliseconds);

    /// <summary>Returns the interpolated (or extrapolated) state for the given client time.</summary>
    public abstract InterpolatedObject InterpolatedState(int timeMilliseconds);
}

/// <summary>
/// A timestamped state snapshot received from the network.
/// Stored as a struct so all <see cref="NetworkInterpolation.received"/> entries are
/// embedded inline in the array — no separate heap allocation per packet.
/// </summary>
internal struct Packet_
{
    internal int timestampMilliseconds;
    internal InterpolatedObject content;
}

/// <summary>
/// Buffers incoming network state snapshots and produces smoothly interpolated
/// (or optionally extrapolated) states for rendering, compensating for network jitter
/// via a configurable playback delay.
/// </summary>
public class NetworkInterpolation : INetworkInterpolation
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Capacity of the backing array. Must be > <see cref="MaxPackets"/>.</summary>
    private const int BufferCapacity = 128;

    /// <summary>
    /// Maximum number of snapshots retained. When the buffer is full the oldest
    /// packet is discarded before the new one is appended.
    /// </summary>
    private const int MaxPackets = 100;

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Interpolation strategy supplied by the caller.</summary>
    internal IInterpolation req;

    /// <summary>When true, the last two known states are extrapolated beyond the newest snapshot.</summary>
    internal bool EXTRAPOLATE;

    /// <summary>Playback is delayed by this many milliseconds to absorb jitter.</summary>
    internal int DELAYMILLISECONDS;

    /// <summary>Maximum duration beyond the newest snapshot that extrapolation is allowed to reach.</summary>
    internal int EXTRAPOLATION_TIMEMILLISECONDS;

    // ── Ring buffer ───────────────────────────────────────────────────────────

    private readonly Packet_[] received = new Packet_[BufferCapacity];
    private int receivedCount;

    public NetworkInterpolation()
    {
        DELAYMILLISECONDS = 200;
        EXTRAPOLATION_TIMEMILLISECONDS = 200;
    }

    /// <summary>
    /// Appends a new snapshot. When the buffer reaches <see cref="MaxPackets"/>,
    /// the oldest entry is discarded via <see cref="Array.Copy"/> (hardware memmove)
    /// rather than a manual element loop.
    /// </summary>
    public override void AddNetworkPacket(InterpolatedObject c, int timeMilliseconds)
    {
        if (receivedCount >= MaxPackets)
        {
            // Shift all entries left by one — discards received[0].
            // Array.Copy uses a native memmove, replacing the previous manual loop.
            Array.Copy(received, 1, received, 0, MaxPackets - 1);
            receivedCount = MaxPackets - 1;
        }

        received[receivedCount++] = new Packet_
        {
            content = c,
            timestampMilliseconds = timeMilliseconds,
        };
    }

    /// <summary>
    /// Returns the interpolated state for <paramref name="timeMilliseconds"/>,
    /// applying the playback delay. When no data has been received, returns null.
    /// </summary>
    public override InterpolatedObject InterpolatedState(int timeMilliseconds)
    {
        if (receivedCount == 0) return null;

        int interpolationTime = timeMilliseconds - DELAYMILLISECONDS;

        int p1, p2;

        if (interpolationTime < received[0].timestampMilliseconds)
        {
            // Before any known data — clamp to the first snapshot.
            p1 = p2 = 0;
        }
        else if (EXTRAPOLATE
              && receivedCount >= 2
              && interpolationTime > received[receivedCount - 1].timestampMilliseconds)
        {
            // Beyond the latest snapshot — extrapolate from the last two.
            p1 = receivedCount - 2;
            p2 = receivedCount - 1;
            interpolationTime = Math.Min(
                interpolationTime,
                received[receivedCount - 1].timestampMilliseconds + EXTRAPOLATION_TIMEMILLISECONDS);
        }
        else
        {
            // Normal case: find the pair that brackets interpolationTime.
            p1 = 0;
            for (int i = 0; i < receivedCount; i++)
            {
                if (received[i].timestampMilliseconds <= interpolationTime)
                    p1 = i;
            }
            p2 = p1 < receivedCount - 1 ? p1 + 1 : p1;
        }

        if (p1 == p2)
            return received[p1].content;

        // Compute fractional progress between p1 and p2.
        float progress =
            (float)(interpolationTime - received[p1].timestampMilliseconds)
            / (received[p2].timestampMilliseconds - received[p1].timestampMilliseconds);

        return req.Interpolate(received[p1].content, received[p2].content, progress);
    }
}

/// <summary>
/// Provides shortest-path angle interpolation for both 256-step and 360-degree representations.
/// </summary>
public class AngleInterpolation
{
    private const int CircleHalf256 = 128;
    private const int CircleFull256 = 256;
    private const float CircleHalf360 = 180f;
    private const float CircleFull360 = 360f;

    /// <summary>
    /// Bias added before modulo to keep results positive for 256-step angles.
    /// Must be an exact multiple of <see cref="CircleFull256"/> so that the
    /// bias does not shift the normalized value.
    /// (32768 = 128 × 256.)
    /// </summary>
    private const int Bias256 = 256 * 128; // 32768

    /// <summary>
    /// Bias added before modulo to keep results positive for 360-degree angles.
    /// Must be an exact multiple of <see cref="CircleFull360"/>.
    /// (36000 = 100 × 360.)
    /// </summary>
    private const float Bias360 = 360f * 100f; // 36000

    /// <summary>
    /// Interpolates between two angles in 256-step (byte) space via the shortest arc.
    /// </summary>
    /// <param name="a">Start angle (0–255).</param>
    /// <param name="b">End angle (0–255).</param>
    /// <param name="progress">Interpolation factor (0 = a, 1 = b).</param>
    /// <returns>Interpolated angle normalised to 0–255.</returns>
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
    /// <param name="progress">Interpolation factor (0 = a, 1 = b).</param>
    /// <returns>Interpolated angle normalised to 0–360.</returns>
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

    /// <summary>
    /// Normalises an angle to [0, 255].
    /// </summary>
    /// <remarks>
    /// The bias must be an exact multiple of 256 so it does not shift the result.
    /// The original code used <c>short.MaxValue / 2 = 16383</c> which is NOT a
    /// multiple of 256 (16383 = 63×256 + 255), causing every normalised value
    /// to be off by −1. Fixed to <see cref="Bias256"/> = 32768 = 128×256.
    /// </remarks>
    private static int NormalizeAngle256(int v) => (v + Bias256) % CircleFull256;

    /// <summary>Normalises an angle to [0, 360).</summary>
    private static float NormalizeAngle360(float v) => (v + Bias360) % CircleFull360;
}