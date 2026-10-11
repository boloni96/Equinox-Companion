namespace EquinoxCompanion;

// Cache derived display data, never game observation or upload work.
public sealed class UiProjectionCache<T>
{
    private object? identity;
    private string stamp="";
    private long expires;
    private bool ready;
    private T value=default!;
    public T Get(object? source,string version,long now,Func<T> build)
    {
        if(ready&&ReferenceEquals(identity,source)&&stamp==version&&now<expires)return value;
        var next=build();value=next;identity=source;stamp=version;expires=now+250;ready=true;
        return value;
    }
}
