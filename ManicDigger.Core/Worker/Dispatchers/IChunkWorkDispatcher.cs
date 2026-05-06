namespace ManicDigger.Worker;

/// <summary>
/// Implement this to wire <see cref="ChunkWorkerPool"/> to your actual chunk
/// generation and tessellation logic. The pool calls <see cref="DispatchAsync"/>
/// from one of its worker threads — all work here is off the main thread.
/// </summary>
public interface IChunkWorkDispatcher
{
    Task DispatchAsync(ChunkWorkItem item, CancellationToken ct);
}