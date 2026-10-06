namespace EquinoxCompanion;

// One writer, at most one waiting snapshot. A later snapshot subsumes earlier edits.
public sealed class LatestSnapshotWriter<T>(Action<T> write) where T : class
{
    private readonly object gate = new();
    private T? pending;
    private Task worker = Task.CompletedTask;
    private Exception? failure;
    public Exception? Failure { get { lock(gate) return failure; } }
    public bool Busy { get { lock(gate) return !worker.IsCompleted; } }
    public void Enqueue(T snapshot)
    {
        lock(gate) { pending=snapshot; if(worker.IsCompleted) worker=Task.Run(Drain); }
    }
    private void Drain()
    {
        while(true)
        {
            T value;
            lock(gate) { if(pending is null) { worker=Task.CompletedTask; return; } value=pending;pending=null; }
            try { write(value);lock(gate)failure=null; }
            catch(Exception e) { lock(gate) { failure=e;pending??=value;worker=Task.CompletedTask;return; } }
        }
    }
    public void Flush(T latest)
    {
        Enqueue(latest);
        Task task;lock(gate)task=worker;
        task.GetAwaiter().GetResult();
        if(Failure is {} e)throw new IOException("Configuration could not be saved",e);
    }
}
