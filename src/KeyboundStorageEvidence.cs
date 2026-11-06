namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<HashSet<uint>>? keyboundStorageTask;
    private HashSet<uint>? keyboundStorageEvidence;
    private object? keyboundStorageSource;
    private int keyboundStorageCount;
    private SyncEvent? keyboundStorageTail;
    private ulong keyboundStorageCharacter;
    private HashSet<uint>? ReadKeyboundStorageEvidence(ulong character)
    {
        if(keyboundStorageTask is {} task){
            if(!task.IsCompleted)return null;
            keyboundStorageTask=null;
            if(task.IsCompletedSuccessfully)keyboundStorageEvidence=task.Result;
            else{_=task.Exception;keyboundStorageSource=null;}
        }
        var rows=config.Discoveries;
        if(keyboundStorageCharacter==character&&ReferenceEquals(keyboundStorageSource,rows)&&keyboundStorageCount==rows.Count&&ReferenceEquals(keyboundStorageTail,rows.LastOrDefault())&&keyboundStorageEvidence!=null)
            return new HashSet<uint>(keyboundStorageEvidence);
        var snapshot=rows.ToArray();var identity=character.ToString();
        var rewards=keyboundOffers!.Select(o=>o.Item).ToHashSet();
        keyboundStorageCharacter=character;keyboundStorageSource=rows;keyboundStorageCount=rows.Count;keyboundStorageTail=rows.LastOrDefault();
        keyboundStorageTask=Task.Run(()=>KeyboundStorageHistory.Read(snapshot,identity,rewards));
        return null;
    }
}
