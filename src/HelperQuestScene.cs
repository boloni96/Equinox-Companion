using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using NativeObject=FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe delegate void HelperSceneDelegate(EventFramework* framework,NativeObject* obj,EventId eventId,short scene,ulong flags,uint* data,byte count);
    private Hook<HelperSceneDelegate>? helperSceneHook;
    private bool helperSceneHookFailed;
    private string helperNativeScene="";
    private readonly HashSet<string> helperNativeScenes=new();
    private void ResetHelperNativeScene(){helperNativeScene="";helperNativeScenes.Clear();}
    private unsafe void UpdateHelperSceneHook()
    {
        var enabled=SharingQuest||helperPermission.Active&&helperPermission.Quest&&!HelperPaused&&!helperPermission.QuestPaused;
        if(enabled&&helperSceneHook==null&&!helperSceneHookFailed){
            try{helperSceneHook=Interop.HookFromAddress<HelperSceneDelegate>(EventFramework.MemberFunctionPointers.ProcessEventPlay,ObserveHelperScene);}
            catch(Exception e){helperSceneHookFailed=true;helperError="Quest scene observer unavailable; quest replay cannot be verified.";Log.Error(e,helperError);}
        }
        if(helperSceneHook!=null){if(enabled&&!helperSceneHook.IsEnabled)helperSceneHook.Enable();else if(!enabled&&helperSceneHook.IsEnabled){helperSceneHook.Disable();ResetHelperNativeScene();}}
    }
    private unsafe void ObserveHelperScene(EventFramework* framework,NativeObject* obj,EventId eventId,short scene,ulong flags,uint* data,byte count)
    {
        try{
            // Recover a missed interaction callback only at a verified native quest-scene start.
            // The native source must resolve to the exact nearby NPC/event object; never guess from current target.
            if(SharingQuest&&!helperRecording&&!helperReplaying&&!relayInteracting&&HelperQuestScenePolicy.Quest(eventId.Id)&&obj!=null){
                var source=Objects.FirstOrDefault(o=>o.Address==(nint)obj);
                if(source!=null){CaptureHelperNpc(source);if(helperRecording)RecordFollowTravel("Helper interaction recovered at quest scene",new {npc=source.Name.TextValue,eventId=eventId.Id,scene});}
            }
            // Observe the event being played, not EventState1, which can be empty during quest scenes.
            if(SharingQuest&&helperRecording)FlushHelperTalk();
            helperNativeScene=eventId.Id==0?"":eventId.Id+":"+scene;
            if(helperNativeScene.Length>0&&helperNativeScenes.Count<128)helperNativeScenes.Add(helperNativeScene);
            if(SharingQuest&&helperRecording&&helperCaptureNpc is {} npc&&HelperQuestScenePolicy.Quest(eventId.Id)&&helperRecorded.Count>0){
                var first=helperRecorded[0];
                if(first.QuestId==0){
                    var manager=QuestManager.Instance();var accepted=manager!=null&&manager->IsQuestAccepted(eventId.Id);
                    helperRecorded[0]=first with {QuestId=eventId.Id,Scene=helperNativeScene,Signature=HelperQuestScenePolicy.Step(accepted,QuestManager.GetQuestSequence(eventId.Id))};
                    RecordFollowTravel("Helper quest identity observed",new {npc=npc.Name,npc.BaseId,eventId=eventId.Id,scene,accepted});
                    EmitHelper("interact",text:"confirmQuestScene",scene:helperNativeScene,questId:eventId.Id);
                }
            }
        }catch(Exception e){Log.Debug(e,"Quest scene capture failed");}
        helperSceneHook!.Original(framework,obj,eventId,scene,flags,data,count);
    }
}
