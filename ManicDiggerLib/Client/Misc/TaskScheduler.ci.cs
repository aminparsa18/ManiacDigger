public class TaskScheduler
{
    public TaskScheduler()
    {
        actions = null;
    }

    private BackgroundAction[] actions;

    public static Action CreateBackgroundAction(Game game, int i, float dt, Action onFinished)
    {
        return () =>
        {
            game.clientmods[i].OnReadOnlyBackgroundThread(game, dt);
            onFinished();
        };
    }

    public void Update(Game game, float dt)
    {
        if (actions == null)
        {
            actions = new BackgroundAction[game.clientmodsCount];
            for (int i = 0; i < game.clientmodsCount; i++)
            {
                int captured = i;
                actions[captured] = new BackgroundAction() { run = CreateBackgroundAction(game, captured, dt, () => { actions[captured].finished = true; }) };
            }
        }

        if (game.platform.MultithreadingAvailable())
        {
            for (int i = 0; i < game.clientmodsCount; i++)
            {
                game.clientmods[i].OnReadOnlyMainThread(game, dt);
            }

            bool allDone = true;
            for (int i = 0; i < game.clientmodsCount; i++)
            {
                if (actions[i] != null && actions[i].active && (!actions[i].finished))
                {
                    allDone = false;
                }
            }

            if (allDone)
            {
                for (int i = 0; i < game.clientmodsCount; i++)
                {
                    game.clientmods[i].OnReadWriteMainThread(game, dt);
                }
                foreach (Action action in game.commitActions)
                {
                    action();
                }
                game.commitActions.Clear();
                for (int i = 0; i < game.clientmodsCount; i++)
                {
                    int captured = i;
                    actions[captured].active = true;
                    actions[captured].finished = false;
                    game.platform.QueueUserWorkItem(CreateBackgroundAction(game, captured, dt, () => actions[captured].finished = true));
                }
            }
        }
        else
        {
            for (int i = 0; i < game.clientmodsCount; i++)
            {
                game.clientmods[i].OnReadOnlyMainThread(game, dt);
            }

            for (int i = 0; i < game.clientmodsCount; i++)
            {
                game.clientmods[i].OnReadOnlyBackgroundThread(game, dt);
            }

            for (int i = 0; i < game.clientmodsCount; i++)
            {
                game.clientmods[i].OnReadWriteMainThread(game, dt);
            }

            foreach (Action action in game.commitActions)
            {
                action();
            }
            game.commitActions.Clear();
        }
    }
}

internal class BackgroundAction
{
    internal bool active;
    internal bool finished;
    internal Action run;
}