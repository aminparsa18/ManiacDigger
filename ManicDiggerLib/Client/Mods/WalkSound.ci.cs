/// <summary>
/// Plays footstep sounds based on the block under the player and movement state.
/// </summary>
public class ModWalkSound : ModBase
{
    private const float StepSoundDuration = 0.4f;
    private const float RandomSoundChance = 40;

    private float walkSoundTimer;
    private int lastWalkSound;

    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        if (game.FollowId() != null) return;

        if (game.soundnow)
            UpdateWalkSound(game, StepSoundDuration / 2);

        if (game.isplayeronground && (game.controls.movedx != 0 || game.controls.movedy != 0))
            UpdateWalkSound(game, args.GetDt());
    }

    internal void UpdateWalkSound(Game game, float dt)
    {
        walkSoundTimer += dt;

        string[] sounds = CurrentWalkSounds(game);
        int soundCount = GetSoundCount(sounds);
        if (soundCount == 0) return;

        if (walkSoundTimer < StepSoundDuration) return;

        walkSoundTimer = 0;
        lastWalkSound = (lastWalkSound + 1) % soundCount;

        if (game.rnd.Next() % 100 < RandomSoundChance)
            lastWalkSound = game.rnd.Next() % soundCount;

        game.PlayAudio(sounds[lastWalkSound]);
    }

    internal static string[] CurrentWalkSounds(Game game)
    {
        int b = game.BlockUnderPlayer();
        return game.d_Data.WalkSound()[b != -1 ? b : 0];
    }

    internal static int GetSoundCount(string[] sounds)
    {
        int count = 0;
        for (int i = 0; i < GameData.SoundCount; i++)
            if (sounds[i] != null) count++;
        return count;
    }
}