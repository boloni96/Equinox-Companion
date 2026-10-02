using System.Collections.Concurrent;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private Hook<EventFramework.Delegates.ProcessEventPlay>? fashionHook;
    private readonly ConcurrentQueue<(DateTimeOffset At, Actor Actor, int Score, int Remaining)> fashionObservations = new();
    private (ulong Character, DateTimeOffset Cycle, int Score, int Remaining)? currentFashion;
    private static DateTimeOffset FashionCycle(DateTimeOffset at)
    {
        var anchor=new DateTimeOffset(2024,1,2,8,0,0,TimeSpan.Zero);
        return anchor.AddDays(7*Math.Floor((at-anchor).TotalDays/7));
    }
    private unsafe void ObserveNpcEvent(EventFramework* framework, NativeGameObject* target, EventId eventId, short scene, ulong flags, uint* values, byte count)
    {
        fashionHook!.Original(framework,target,eventId,scene,flags,values,count);
        if (!config.SyncActivities || !Player.IsLoaded || target == null || target->BaseId != 1025176 || values == null || fashionObservations.Count >= 16) return;
        try
        {
            var now=DateTimeOffset.UtcNow;var cycle=FashionCycle(now);
            if(currentFashion?.Character!=Player.ContentId||currentFashion?.Cycle!=cycle)currentFashion=null;
            // Masked Rose sends the actual current-week record on the initial interaction.
            if(scene==1&&count>=2&&values[0]<=100&&values[1]<=4)currentFashion=(Player.ContentId,cycle,(int)values[0],(int)values[1]);
            else if(scene==2&&count>=1&&values[0]<=100&&currentFashion is {} previous)currentFashion=(previous.Character,cycle,Math.Max(previous.Score,(int)values[0]),previous.Remaining);
            else if(scene==5&&count>=1&&values[0]<=4&&currentFashion is {} judged)currentFashion=(judged.Character,cycle,judged.Score,(int)values[0]);
            else return;
            if(currentFashion is {} info)fashionObservations.Enqueue((now,ReadActor(),info.Score,info.Remaining));
        }
        catch(Exception ex){errorJournal.Record("fashion","Fashion observation deferred.",exceptionType:ex.GetType().Name);}
    }
    private void DrainFashionObservations()
    {
        while(fashionObservations.TryDequeue(out var observed))
            KeepDiscovery(new(Guid.NewGuid().ToString("N"),"fashion.observed",observed.At,observed.Actor,null,
                Fashion:new(observed.Score,observed.Remaining,0,FashionCycle(observed.At).ToString("O"))));
    }
}
