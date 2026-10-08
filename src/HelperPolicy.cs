using System.Numerics;
using System.Security.Cryptography;
using System.Text;
namespace EquinoxCompanion;
public sealed record HelperNpc(string Conversation,uint BaseId,string Name,uint Territory,uint Map,uint World,FollowTravelPosition Position,FollowTravelPosition Approach,float Facing);
public sealed record HelperAction(string Id,string Name,uint World,string Kind,long SentAt,HelperNpc Npc,string Text="",string Signature="",string Addon="",string Scene="",string[]? Sessions=null,long Sequence=0,uint QuestId=0,ushort FateId=0,int FateStart=0,HelperAction[]? Steps=null);
public sealed record HelperFollower(string Id,string Name,uint World,string Status,bool Quest,bool Skip,bool Paused,string Control,long Updated,bool QuestPaused=false);
public sealed record HelperReply(int Protocol=0,string Control="",HelperAction[]? Actions=null,HelperFollower[]? Followers=null,bool QuestPaused=false,long TravelAfter=0);
public sealed class HelperPermission
{
    public bool Active {get;private set;}
    public bool Quest {get;private set;}
    public bool Skip {get;private set;}
    public bool LeaderPaused {get;private set;}
    public bool Paused=>LeaderPaused;
    public bool QuestPaused {get;private set;}
    public void SetQuestPause(bool paused){if(Active)QuestPaused=paused;}
    public void Start(bool quest,bool skip){Active=true;Quest=quest;Skip=quest&&skip;LeaderPaused=false;QuestPaused=false;}
    public void Stop(){Active=false;Quest=false;Skip=false;LeaderPaused=false;QuestPaused=false;}
    public void Control(string command){if(!Active)return;if(command=="stop")Stop();else if(command=="pause")LeaderPaused=true;else if(command=="resume")LeaderPaused=false;}
    public bool Allows(string kind)=>Active&&!Paused&&!QuestPaused&&Quest&&(kind!="skip"||Skip);
}
public static class HelperPolicy
{
    public static uint QuestRowId(uint id)=>id==0?0:id<65536?id+65536:id;
    public static bool HasLeaderRole(IEnumerable<HelperFollower> followers)=>followers.Any(f=>f.Control!="stop");
    public static Vector3 Right(Vector3 position,float facing)=>position+new Vector3(MathF.Cos(facing),0,-MathF.Sin(facing))*.9f;
    public static string Signature(IEnumerable<string> lines)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",lines))));
    public static int Match(IEnumerable<string> options,string text){var all=options.ToArray();return all.Count(x=>x==text)==1?Array.IndexOf(all,text):-1;}
    public static bool Fresh(HelperAction a,long now,long started)=>a.SentAt>=started&&a.SentAt<=now+5000&&now-a.SentAt<120000;
    public static bool Audience(HelperFollower f,long now,bool skip=false)=>f.Quest&&!f.QuestPaused&&(!skip||f.Skip)&&!f.Paused&&f.Control=="resume"&&now-f.Updated<15000;
    public static bool SceneMatches(string expected,string actual)=>expected.Length>0&&expected==actual;
}

public sealed record HelperDialogueStep(string Text,string Signature,string Scene);
public sealed class HelperDialogueCapture
{
    private HelperDialogueStep? current;
    public HelperDialogueStep? Observe(string text,string signature,string scene)
    {
        if(signature.Length==0)return null;
        if(current?.Signature==signature&&current.Scene==scene)return null;
        var previous=current;current=new(text,signature,scene);return previous;
    }
    public HelperDialogueStep? Finish(){var previous=current;current=null;return previous;}
    public void Reset()=>current=null;
}

// A completed leader conversation is replayed in order, retaining its observed spacing.
public static class HelperConversationPolicy
{
    public static bool MayRecord(bool recording,bool sharing)=>recording&&sharing;
    public static bool CommitAtNextNpc(bool recording,bool failed,bool accepting,bool visible)=>recording&&!failed&&!accepting&&!visible;
    public static bool RetryTalk(int attempts,double milliseconds)=>attempts<3&&milliseconds>=2000;
    public static string NormalizePrompt(string text)=>System.Text.RegularExpressions.Regex.Replace(text,@"\s+"," ").Trim();
    public static int Delay(long previous,long current)=>(int)Math.Clamp(current-previous,450,600000);
    public static bool Cancellation(HelperAction a)=>a.Kind=="acceptQuest"&&a.Scene=="decline"||a.Kind=="eventReplay"&&a.Scene=="no";
    public static bool ValidSteps(HelperAction batch)=>batch.Steps is {Length:>0 and <=128} steps&&steps[0].Kind=="interact"&&steps.All(a=>a.Steps==null&&a.Npc==batch.Npc&&a.Name==batch.Name&&a.World==batch.World&&a.Kind is "interact" or "talk" or "choice" or "skip" or "acceptQuest" or "eventReplay")&&steps.Select(a=>a.Id).Distinct().Count()==steps.Length&&steps.Zip(steps.Skip(1)).All(p=>p.First.SentAt<=p.Second.SentAt);
}

public static class HelperSessionPolicy
{
    public static bool AcceptTravel(bool paused,long sentAt,long started,long discardedThrough)=>!paused&&sentAt>=started&&sentAt>discardedThrough;
}

// NPC identity alone is not quest evidence: vendors are EventNpc objects too.
public static class HelperQuestScope
{
    public static uint Evidence(IEnumerable<HelperAction> actions)
    {
        foreach(var a in actions){
            if(a.Kind=="acceptQuest"&&a.Addon=="JournalAccept"&&a.Scene is "offer" or "confirmed" or "decline"&&a.QuestId>=65536)return a.QuestId;
            if(a.Kind=="eventReplay"&&a.Addon=="SelectYesno"&&a.Npc.Name=="Kipih Jakkya"&&a.Npc.Territory==130&&a.QuestId>=65536&&
                a.Scene is "yes" or "no" or "checked" or "unchecked"&&
                (a.Text=="Do you wish to replay the event?"||a.Text.StartsWith("If you proceed, the following quest(s) will be rendered incomplete:",StringComparison.Ordinal)&&
                 new[]{"The Man in Black","In the Dark of Night","Messenger of the Winds","The Ironworks Vendor","The Recompense Officer"}.All(a.Text.Contains)))return a.QuestId;
            // Native quest handlers have event type 1; this also covers in-progress quest dialogue and turn-ins.
            var colon=a.Scene.IndexOf(':');
            if(a.Kind is "talk" or "choice" or "skip"&&colon>0&&uint.TryParse(a.Scene[..colon],out var eventId)&&(eventId>>16)==1&&(eventId&65535)!=0)return eventId;
        }
        return 0;
    }
}
