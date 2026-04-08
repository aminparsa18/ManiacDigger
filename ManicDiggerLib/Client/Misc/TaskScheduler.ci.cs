/// <summary>
/// Schedules and coordinates per-frame execution of client mod lifecycle hooks,
/// dispatching background work to worker threads when multithreading is available
/// and falling back to sequential execution otherwise.
/// </summary>
public class TaskScheduler
{
    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    /// <summary>
    /// Per-mod background task state. Initialised once via <see cref="Initialise"/>.
    /// </summary>
    private BackgroundAction[] _actions;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initialises the background action slots for all currently registered client mods.
    /// Must be called once before the first <see cref="Update"/> call.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    public void Initialise(Game game)
    {
        _actions = new BackgroundAction[game.clientmods.Count];
        for (int i = 0; i < game.clientmods.Count; i++)
            _actions[i] = new BackgroundAction();
    }

    /// <summary>
    /// Drives the mod update lifecycle for one frame.
    /// When multithreading is available, background work runs on worker threads
    /// while the main thread proceeds; all read-write work is deferred until
    /// all background tasks from the previous frame have completed.
    /// When multithreading is unavailable, all hooks run sequentially.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="dt">Delta time in seconds since the last frame.</param>
    public void Update(Game game, float dt)
    {
        if (game.platform.MultithreadingAvailable())
            UpdateMultithreaded(game, dt);
        else
            UpdateSingleThreaded(game, dt);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs main-thread read-only hooks, then — if all background tasks from the
    /// previous frame are done — flushes commit actions, runs read-write hooks,
    /// and dispatches new background tasks.
    /// </summary>
    private void UpdateMultithreaded(Game game, float dt)
    {
        RunReadOnlyMainThread(game, dt);

        if (!AllBackgroundTasksFinished(game))
            return;

        RunReadWriteMainThread(game, dt);
        FlushCommitActions(game);
        DispatchBackgroundTasks(game, dt);
    }

    /// <summary>
    /// Runs all mod hooks sequentially on the main thread:
    /// read-only main, read-only background, then read-write main.
    /// </summary>
    private static void UpdateSingleThreaded(Game game, float dt)
    {
        RunReadOnlyMainThread(game, dt);

        for (int i = 0; i < game.clientmods.Count; i++)
            game.clientmods[i].OnReadOnlyBackgroundThread(game, dt);

        RunReadWriteMainThread(game, dt);
        FlushCommitActions(game);
    }

    /// <summary>
    /// Returns <c>true</c> if no background task is still active and unfinished.
    /// </summary>
    private bool AllBackgroundTasksFinished(Game game)
    {
        for (int i = 0; i < game.clientmods.Count; i++)
        {
            BackgroundAction action = _actions[i];
            if (action.Active && !action.Finished)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Marks each background slot as active and queues its work item on the thread pool.
    /// </summary>
    private void DispatchBackgroundTasks(Game game, float dt)
    {
        for (int i = 0; i < game.clientmods.Count; i++)
        {
            int captured = i;
            _actions[captured].Active = true;
            _actions[captured].Finished = false;
            game.platform.QueueUserWorkItem(
                CreateBackgroundAction(game, captured, dt, () => _actions[captured].Finished = true));
        }
    }

    /// <summary>Calls <c>OnReadOnlyMainThread</c> on every registered client mod.</summary>
    private static void RunReadOnlyMainThread(Game game, float dt)
    {
        for (int i = 0; i < game.clientmods.Count; i++)
            game.clientmods[i].OnReadOnlyMainThread(game, dt);
    }

    /// <summary>Calls <c>OnReadWriteMainThread</c> on every registered client mod.</summary>
    private static void RunReadWriteMainThread(Game game, float dt)
    {
        for (int i = 0; i < game.clientmods.Count; i++)
            game.clientmods[i].OnReadWriteMainThread(game, dt);
    }

    /// <summary>Executes all pending commit actions then clears the queue.</summary>
    private static void FlushCommitActions(Game game)
    {
        foreach (Action action in game.commitActions)
            action();
        game.commitActions.Clear();
    }

    /// <summary>
    /// Builds an <see cref="Action"/> that invokes a mod's background hook
    /// then calls <paramref name="onFinished"/> when complete.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="modIndex">Index of the mod in <c>game.clientmods</c>.</param>
    /// <param name="dt">Delta time passed through to the background hook.</param>
    /// <param name="onFinished">Callback invoked on the worker thread when the hook returns.</param>
    /// <returns>A self-contained <see cref="Action"/> safe to queue on the thread pool.</returns>
    private static Action CreateBackgroundAction(Game game, int modIndex, float dt, Action onFinished)
    {
        return () =>
        {
            game.clientmods[modIndex].OnReadOnlyBackgroundThread(game, dt);
            onFinished();
        };
    }
}

/// <summary>
/// Tracks the execution state of a single background task slot.
/// </summary>
internal class BackgroundAction
{
    /// <summary>
    /// Whether this slot has been dispatched to the thread pool and not yet reset.
    /// </summary>
    internal bool Active;

    /// <summary>
    /// Whether the background work has completed. Set to <c>true</c> by the worker thread.
    /// </summary>
    internal bool Finished;
}