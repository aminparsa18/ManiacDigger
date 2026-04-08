/// <summary>
/// A client-side mod that records a sequence of camera waypoints and plays
/// the camera smoothly through them using Catmull-Rom spline interpolation.
/// Optionally captures each frame to an AVI video file during playback.
/// </summary>
public class ModAutoCamera : ModBase
{
    /// <summary>Target frame rate for AVI recording (frames per second).</summary>
    private const int Framerate = 60;

    /// <summary>Maximum number of camera waypoints that can be stored.</summary>
    private const int MaxCameraPoints = 256;

    /// <summary>
    /// Active AVI writer. <see langword="null"/> when not recording.
    /// </summary>
    private AviWriterCi _avi;

    /// <summary>Freemove level that was active before playback started, restored on stop.</summary>
    private int _previousFreemove;

    /// <summary>Player position saved before playback, restored when playback stops.</summary>
    private float _previousPositionX, _previousPositionY, _previousPositionZ;

    /// <summary>Player orientation saved before playback, restored when playback stops.</summary>
    private float _previousOrientationX, _previousOrientationY, _previousOrientationZ;

    /// <summary>
    /// World-units per second at which the camera travels along the path.
    /// Derived from total path length divided by the requested playback duration.
    /// </summary>
    private float _playingSpeed;

    /// <summary>
    /// Ratio of real-time seconds to recorded-video seconds.
    /// A value of 2 means one second of real playback becomes 0.5 s of video (2× speed).
    /// </summary>
    private float _recSpeed;

    /// <summary>Accumulated real-time delta used to decide when to capture the next AVI frame.</summary>
    private float _writeAccum;

    /// <summary>
    /// <see langword="true"/> after the first frame following playback start has been skipped.
    /// The first frame is skipped because the screen has not been redrawn yet at that point.
    /// </summary>
    private bool _firstFrameDone;

    /// <summary>Reference to the client mod manager, set in <see cref="Start"/>.</summary>
    private ClientModManager _m;

    /// <summary>
    /// Floating-point literal <c>1.0f</c> stored in a field to prevent the Cito
    /// transpiler from truncating integer division expressions to integer arithmetic.
    /// </summary>
    private float _one;

    /// <summary>Fixed-size pool of camera waypoints.</summary>
    private CameraPoint[] _cameraPoints;

    /// <summary>Number of waypoints currently stored in <see cref="_cameraPoints"/>.</summary>
    private int _cameraPointsCount;

    /// <summary>
    /// Elapsed real-time seconds since playback began.
    /// A value of <c>-1</c> means the camera is not currently playing.
    /// </summary>
    private float _playingTime;

    /// <inheritdoc/>
    public override void Start(ClientModManager modmanager)
    {
        _m = modmanager;
        _one = 1;
        _cameraPoints = new CameraPoint[MaxCameraPoints];
        _cameraPointsCount = 0;
        _playingTime = -1;
    }

    /// <inheritdoc/>
    public override bool OnClientCommand(Game game, ClientCommandArgs args)
    {
        if (args.command != "cam")
        {
            return false;
        }

        string[] arguments = args.arguments.Split(" ");

        if (args.arguments.Trim() == "")
        {
            PrintHelp();
            return true;
        }

        switch (arguments[0])
        {
            case "p":
                AddPoint();
                break;

            case "start":
            case "play":
            case "rec":
                StartPlayback(arguments);
                break;

            case "stop":
                _m.DisplayNotification("Camera stopped.");
                Stop();
                break;

            case "clear":
                _m.DisplayNotification("Camera points cleared.");
                _cameraPointsCount = 0;
                Stop();
                break;

            case "save":
                SavePointsToClipboard();
                break;

            case "load":
                if (arguments.Length >= 2)
                {
                    LoadPointsFromString(arguments[1]);
                }
                break;
        }

        return true;
    }

    /// <inheritdoc/>
    public override void OnNewFrame(Game game, NewFrameEventArgs args)
    {
        if (_playingTime == -1)
        {
            return;
        }

        float dt = args.GetDt();
        _playingTime += dt;

        UpdateAvi(dt);

        float playingDist = _playingTime * _playingSpeed;

        // Walk the segments to find which one contains the current distance.
        float distA = 0;
        int foundPoint = -1;
        for (int i = 0; i < _cameraPointsCount - 1; i++)
        {
            float segmentDist = Distance(_cameraPoints[i], _cameraPoints[i + 1]);
            if (playingDist >= distA && playingDist < distA + segmentDist)
            {
                foundPoint = i;
                break;
            }
            distA += segmentDist;
        }

        if (foundPoint == -1)
        {
            // Past the end of the path — playback is complete.
            Stop();
            return;
        }

        ApplyCatmullRomPosition(foundPoint, distA, playingDist);
    }

    /// <summary>Displays the in-game help text for all <c>.cam</c> sub-commands.</summary>
    private void PrintHelp()
    {
        _m.DisplayNotification("&6AutoCamera help.");
        _m.DisplayNotification("&6.cam p&f - add a point to path");
        _m.DisplayNotification("&6.cam start [real seconds]&f - play the path");
        _m.DisplayNotification("&6.cam rec [real seconds] [video seconds]&f - play and record to .avi file");
        _m.DisplayNotification("&6.cam stop&f - stop playing and recording");
        _m.DisplayNotification("&6.cam clear&f - remove all points from path");
        _m.DisplayNotification("&6.cam save&f - copy path points to clipboard");
        _m.DisplayNotification("&6.cam load [points]&f - load path points");
    }

    /// <summary>
    /// Captures the player's current position and orientation as a new waypoint
    /// and appends it to <see cref="_cameraPoints"/>.
    /// </summary>
    private void AddPoint()
    {
        CameraPoint point = new()
        {
            positionGlX = _m.GetLocalPositionX(),
            positionGlY = _m.GetLocalPositionY(),
            positionGlZ = _m.GetLocalPositionZ(),
            orientationGlX = _m.GetLocalOrientationX(),
            orientationGlY = _m.GetLocalOrientationY(),
            orientationGlZ = _m.GetLocalOrientationZ()
        };
        _cameraPoints[_cameraPointsCount++] = point;
        _m.DisplayNotification("Point defined.");
    }

    /// <summary>
    /// Validates preconditions and then begins camera playback, optionally opening
    /// an AVI file for recording.
    /// </summary>
    /// <param name="arguments">
    /// Tokenised command arguments.
    /// <list type="bullet">
    ///   <item><description><c>arguments[0]</c> — sub-command (<c>"start"</c>, <c>"play"</c>, or <c>"rec"</c>).</description></item>
    ///   <item><description><c>arguments[1]</c> (optional) — total real-time duration in seconds.</description></item>
    ///   <item><description><c>arguments[2]</c> (optional, <c>"rec"</c> only) — desired video duration in seconds.</description></item>
    /// </list>
    /// </param>
    private void StartPlayback(string[] arguments)
    {
        if (!_m.IsFreemoveAllowed())
        {
            _m.DisplayNotification("Free move not allowed.");
            return;
        }

        if (_cameraPointsCount == 0)
        {
            _m.DisplayNotification("No points defined. Enter points with \".cam p\" command.");
            return;
        }

        _playingSpeed = 1;
        float totalRecTime = -1;

        if (arguments[0] == "rec")
        {
            if (arguments.Length >= 3)
            {
                totalRecTime = float.Parse(arguments[2]);
            }

            _avi = _m.AviWriterCreate();
            _avi.Open(
                string.Format("{0}.avi", string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now)),
                Framerate,
                _m.GetWindowWidth(),
                _m.GetWindowHeight());
        }

        if (arguments.Length >= 2)
        {
            float totalTime = float.Parse(arguments[1]);
            _playingSpeed = TotalDistance() / totalTime;
            _recSpeed = totalRecTime == -1 ? 10 : totalTime / totalRecTime;
        }

        _playingTime = 0;
        _firstFrameDone = false;

        // Save current camera state so it can be restored when playback ends.
        _previousPositionX = _m.GetLocalPositionX();
        _previousPositionY = _m.GetLocalPositionY();
        _previousPositionZ = _m.GetLocalPositionZ();
        _previousOrientationX = _m.GetLocalOrientationX();
        _previousOrientationY = _m.GetLocalOrientationY();
        _previousOrientationZ = _m.GetLocalOrientationZ();

        _m.ShowGui(0);
        _previousFreemove = _m.GetFreemove();
        _m.SetFreemove(FreemoveLevelEnum.Noclip);
        _m.EnableCameraControl(false);
    }

    /// <summary>
    /// Serialises all current waypoints to a compact comma-separated string and
    /// copies it to the system clipboard so the path can be shared or reloaded later.
    /// </summary>
    /// <remarks>
    /// Format: <c>1,x0,y0,z0,rx0,ry0,rz0,x1,y1,z1,rx1,ry1,rz1,…</c>
    /// Positions are stored as integer centimetres; orientations as integer milliradians.
    /// </remarks>
    private void SavePointsToClipboard()
    {
        string s = "1,";
        for (int i = 0; i < _cameraPointsCount; i++)
        {
            CameraPoint point = _cameraPoints[i];
            s = string.Format("{0}{1},", s, ((int)(point.positionGlX * 100)).ToString());
            s = string.Format("{0}{1},", s, ((int)(point.positionGlY * 100)).ToString());
            s = string.Format("{0}{1},", s, ((int)(point.positionGlZ * 100)).ToString());
            s = string.Format("{0}{1},", s, ((int)(point.orientationGlX * 1000)).ToString());
            s = string.Format("{0}{1},", s, ((int)(point.orientationGlY * 1000)).ToString());
            s = string.Format("{0}{1}", s, ((int)(point.orientationGlZ * 1000)).ToString());
            if (i != _cameraPointsCount - 1)
            {
                s = string.Format("{0},", s);
            }
        }
        Clipboard.SetText(s);
        _m.DisplayNotification("Camera points copied to clipboard.");
    }

    /// <summary>
    /// Parses a comma-separated waypoint string (as produced by <see cref="SavePointsToClipboard"/>)
    /// and replaces the current set of waypoints with the decoded values.
    /// </summary>
    /// <param name="data">
    /// The encoded waypoint string. Expected format:
    /// <c>1,x0,y0,z0,rx0,ry0,rz0,x1,…</c>
    /// </param>
    private void LoadPointsFromString(string data)
    {
        string[] points = data.Split(",");
        int n = (points.Length - 1) / 6;
        _cameraPointsCount = 0;
        for (int i = 0; i < n; i++)
        {
            CameraPoint point = new()
            {
                positionGlX = _one * int.Parse(points[1 + i * 6 + 0]) / 100,
                positionGlY = _one * int.Parse(points[1 + i * 6 + 1]) / 100,
                positionGlZ = _one * int.Parse(points[1 + i * 6 + 2]) / 100,
                orientationGlX = _one * int.Parse(points[1 + i * 6 + 3]) / 1000,
                orientationGlY = _one * int.Parse(points[1 + i * 6 + 4]) / 1000,
                orientationGlZ = _one * int.Parse(points[1 + i * 6 + 5]) / 1000
            };
            _cameraPoints[_cameraPointsCount++] = point;
        }
        _m.DisplayNotification(string.Format("Camera points loaded: {0}", n.ToString()));
    }

    /// <summary>
    /// Stops camera playback, restores the player's previous position, orientation,
    /// and freemove level, re-enables camera control and the HUD, and closes any
    /// open AVI writer.
    /// </summary>
    private void Stop()
    {
        _m.ShowGui(1);
        _m.EnableCameraControl(true);

        if (_playingTime != -1)
        {
            _m.SetFreemove(_previousFreemove);
            _m.SetLocalPosition(_previousPositionX, _previousPositionY, _previousPositionZ);
            _m.SetLocalOrientation(_previousOrientationX, _previousOrientationY, _previousOrientationZ);
        }

        _playingTime = -1;
        _avi?.Close();
        _avi = null;
    }

    /// <summary>
    /// Evaluates the Catmull-Rom spline at the current playback distance and applies
    /// the resulting position and orientation to the local camera.
    /// </summary>
    /// <param name="segmentIndex">Index of the first waypoint of the active segment.</param>
    /// <param name="segmentStartDist">Cumulative path distance at the start of that segment.</param>
    /// <param name="playingDist">Current cumulative distance along the entire path.</param>
    private void ApplyCatmullRomPosition(int segmentIndex, float segmentStartDist, float playingDist)
    {
        CameraPoint a = _cameraPoints[segmentIndex];
        CameraPoint b = _cameraPoints[segmentIndex + 1];
        CameraPoint aMinus = segmentIndex - 1 >= 0 ? _cameraPoints[segmentIndex - 1] : a;
        CameraPoint bPlus = segmentIndex + 2 < _cameraPointsCount ? _cameraPoints[segmentIndex + 2] : b;

        float t = (playingDist - segmentStartDist) / Distance(a, b);

        float x = CatmullRom(t, aMinus.positionGlX, a.positionGlX, b.positionGlX, bPlus.positionGlX);
        float y = CatmullRom(t, aMinus.positionGlY, a.positionGlY, b.positionGlY, bPlus.positionGlY);
        float z = CatmullRom(t, aMinus.positionGlZ, a.positionGlZ, b.positionGlZ, bPlus.positionGlZ);
        _m.SetLocalPosition(x, y, z);

        float orientX = CatmullRom(t, aMinus.orientationGlX, a.orientationGlX, b.orientationGlX, bPlus.orientationGlX);
        float orientY = CatmullRom(t, aMinus.orientationGlY, a.orientationGlY, b.orientationGlY, bPlus.orientationGlY);
        float orientZ = CatmullRom(t, aMinus.orientationGlZ, a.orientationGlZ, b.orientationGlZ, bPlus.orientationGlZ);
        _m.SetLocalOrientation(orientX, orientY, orientZ);
    }

    /// <summary>
    /// Conditionally captures a screenshot and appends it to the AVI file.
    /// Called every frame during playback. The capture is rate-controlled by
    /// <see cref="_recSpeed"/> so the video plays back at the intended duration.
    /// </summary>
    /// <param name="dt">Real-time delta for the current frame in seconds.</param>
    private void UpdateAvi(float dt)
    {
        if (_avi == null)
        {
            return;
        }

        if (!_firstFrameDone)
        {
            // Skip the very first frame: the screen has not been redrawn yet after
            // the camera was repositioned, so capturing it would produce a stale image.
            _firstFrameDone = true;
            return;
        }

        _writeAccum += dt;
        if (_writeAccum >= _one / Framerate * _recSpeed)
        {
            _writeAccum -= _one / Framerate * _recSpeed;

            var bmp = _m.GrabScreenshot();
            _avi.AddFrame(bmp);
            bmp.Dispose();
        }
    }

    /// <summary>
    /// Returns the total arc length of the camera path by summing the straight-line
    /// distances between all consecutive waypoints.
    /// </summary>
    /// <returns>Total path length in world units.</returns>
    private float TotalDistance()
    {
        float total = 0;
        for (int i = 0; i < _cameraPointsCount - 1; i++)
        {
            total += Distance(_cameraPoints[i], _cameraPoints[i + 1]);
        }
        return total;
    }

    /// <summary>
    /// Returns the Euclidean distance between two waypoints using only their
    /// position components (orientation is not factored in).
    /// </summary>
    /// <param name="a">First waypoint.</param>
    /// <param name="b">Second waypoint.</param>
    /// <returns>Distance in world units.</returns>
    private static float Distance(CameraPoint a, CameraPoint b)
    {
        float dx = a.positionGlX - b.positionGlX;
        float dy = a.positionGlY - b.positionGlY;
        float dz = a.positionGlZ - b.positionGlZ;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Evaluates the Catmull-Rom spline for a single scalar coordinate.
    /// </summary>
    /// <remarks>
    /// The four control points are <paramref name="p0"/> (one before the segment start),
    /// <paramref name="p1"/> (segment start), <paramref name="p2"/> (segment end), and
    /// <paramref name="p3"/> (one after the segment end).
    /// When the segment is at the beginning or end of the path the missing neighbour
    /// is replaced by the nearest endpoint, giving a natural clamped result.
    /// <para>
    /// Implementation adapted from:
    /// http://stackoverflow.com/questions/939874/is-there-a-java-library-with-3d-spline-functions/2623619#2623619
    /// </para>
    /// </remarks>
    /// <param name="t">Interpolation parameter in the range [0, 1].</param>
    /// <param name="p0">Control point before the segment start.</param>
    /// <param name="p1">Segment start value.</param>
    /// <param name="p2">Segment end value.</param>
    /// <param name="p3">Control point after the segment end.</param>
    /// <returns>Interpolated scalar value at <paramref name="t"/>.</returns>
    public static float CatmullRom(float t, float p0, float p1, float p2, float p3)
    {
        float one = 1;
        return (one / 2) * (
              (2 * p1)
            + (-p0 + p2) * t
            + (2 * p0 - 5 * p1 + 4 * p2 - p3) * (t * t)
            + (-p0 + 3 * p1 - 3 * p2 + p3) * (t * t * t));
    }
}

/// <summary>
/// Stores the position and orientation of a single camera waypoint on a recorded path.
/// Positions are in OpenGL world-space coordinates; orientations are Euler angles in radians.
/// </summary>
public class CameraPoint
{
    /// <summary>World-space X coordinate of the camera.</summary>
    internal float positionGlX;

    /// <summary>World-space Y coordinate of the camera.</summary>
    internal float positionGlY;

    /// <summary>World-space Z coordinate of the camera.</summary>
    internal float positionGlZ;

    /// <summary>Camera orientation around the X axis (pitch), in radians.</summary>
    internal float orientationGlX;

    /// <summary>Camera orientation around the Y axis (yaw), in radians.</summary>
    internal float orientationGlY;

    /// <summary>Camera orientation around the Z axis (roll), in radians.</summary>
    internal float orientationGlZ;
}