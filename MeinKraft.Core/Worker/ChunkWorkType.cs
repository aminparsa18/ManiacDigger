namespace MeinKraft.Worker;

public enum ChunkWorkType
{
    Generate,       // world gen for a new chunk
    Tessellate,     // rebuild mesh after block change
    RelightBetweenChunks,    // LightBetweenChunks pass only
    RelightFull,    // LightBase + LightBetweenChunks
}