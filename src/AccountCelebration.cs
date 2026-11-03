using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<string,bool> accountCelebrationObserved=[];
    private readonly Queue<(string Key,string Account,string Activity)> accountCelebrations=[];
    private (string Key,string Account,string Activity)? accountCelebration;
    private double accountCelebrationElapsed,accountCelebrationLastFrame;
    private DateTimeOffset eventCelebrationNext;
    private string accountCelebrationPairing="";
    private static string CelebrationKey(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private void ResetCelebrationScope()
    {
        if(accountCelebrationPairing==config.PairingKey)return;
        accountCelebrationPairing=config.PairingKey;accountCelebrationObserved.Clear();accountCelebrations.Clear();accountCelebration=null;eventCelebrationNext=default;
    }
    private void QueueAccountCelebration(string identity,string account,string activity)
    {
        ResetCelebrationScope();
        var key=CelebrationKey(config.PairingKey+"|"+identity);
        config.AccountCelebrationsShown??=[];
        if(config.AccountCelebrationsShown.Contains(key)||accountCelebration?.Key==key||accountCelebrations.Any(c=>c.Key==key))return;
        accountCelebrations.Enqueue((key,account,activity));
    }
    private void ObserveAccountFashionCompletion()
    {
        if(!adventureFresh||!Player.IsLoaded)return;
        ResetCelebrationScope();
        var current=CurrentSharedCharacter();if(current==null||string.IsNullOrEmpty(current.AccountId))return;
        var person=(config.SharedRoster?.People??[]).SingleOrDefault(p=>p.Characters.Any(c=>c.Id==current.Id));
        if(person==null)return;
        var characters=person.Characters.Where(c=>c.AccountId==current.AccountId).ToArray();
        var ids=characters.Select(c=>c.Id).ToHashSet();
        var now=DateTimeOffset.UtcNow;if(!FashionCompletion.IsOpen(now))return;
        foreach(var board in adventureBoards){
            var tasks=board.Stops.SelectMany(s=>s.Tasks).Where(t=>AdventurePolicy.IsFashion(t)&&ids.Contains(AdventurePolicy.TaskCharacterId(t,characters))).DistinctBy(t=>t.Id).ToArray();
            if(tasks.Length==0)continue;
            var key=$"fashion|{person.Id}|{current.AccountId}|{board.Id}|{board.Generation}|{FashionCompletion.Cycle(now):O}|"+string.Join(",",tasks.Select(t=>t.Id).Order());
            var complete=AccountCelebrationPolicy.Complete(tasks.Length,tasks.Count(t=>t.Done));
            bool? previous=accountCelebrationObserved.TryGetValue(key,out var was)?was:null;
            accountCelebrationObserved[key]=complete;
            if(AccountCelebrationPolicy.Transition(previous,complete))QueueAccountCelebration(key,current.Account,"Fashion Report");
        }
    }
    private void CelebrateEventAccount()=>ObserveEventAccountCompletion();
    private unsafe void ObserveEventAccountCompletion()
    {
        if(!Player.IsLoaded||keyboundBuying)return;
        var current=CurrentSharedCharacter();if(current==null||string.IsNullOrEmpty(current.AccountId))return;
        var person=(config.SharedRoster?.People??[]).SingleOrDefault(p=>p.Characters.Any(c=>c.Id==current.Id));
        if(person==null)return;
        // Wanted rewards define the account's event participants, independently of
        // which characters were selected for an automatic routine.
        var characters=person.Characters.Where(c=>c.AccountId==current.AccountId&&c.KeyboundWishlist?.Any(w=>w.Wanted)==true).DistinctBy(c=>c.Id).ToArray();
        if(characters.Length==0)return;
        var identity=$"keybound-5466-wishlist-v2|{person.Id}|{current.AccountId}|"+string.Join(",",characters.Select(c=>c.Id).Order());
        if(config.AccountCelebrationsShown?.Contains(CelebrationKey(config.PairingKey+"|"+identity))==true)return;
        var tasks=adventureBoards.SelectMany(b=>b.Stops).SelectMany(s=>s.Tasks).Where(t=>t.Rule?.Kind=="event"&&t.Rule.Quests?.Any(q=>QuestToolsPolicy.ShortId(q)==QuestToolsPolicy.KeywardBound)==true).ToArray();
        var quests=DiscoveryReads().Kind("collection.observed").Where(e=>e.Collection?.Category=="quest").ToArray();
        foreach(var c in characters){
            var quest=c.Id==current.Id?QuestManager.IsQuestComplete(QuestToolsPolicy.KeywardBound):
                tasks.Any(t=>t.Done&&t.ServerConfirmed&&AdventurePolicy.TaskCharacterId(t,characters)==c.Id)||
                (!string.IsNullOrEmpty(c.ContentId)&&quests.Any(e=>e.Actor.ContentId==c.ContentId&&e.Collection!.Unlocked.Any(q=>QuestToolsPolicy.ShortId(q)==QuestToolsPolicy.KeywardBound)));
            if(!AccountCelebrationPolicy.EventGoalComplete(quest,c.KeyboundWishlist?.Select(w=>(w.Wanted,w.Obtained))))return;
        }
        // Catch up once when the first synchronized observation is already complete.
        // The durable shown key prevents repeating this on login or every poll.
        QueueAccountCelebration(identity,current.Account,"Keybound Brawler + reward list");
    }
    private void DrawAccountCelebration()
    {
        // Foreground drawing has no window, focus, input capture, or saved size.
        ResetCelebrationScope();
        var now=ImGui.GetTime();
        var delta=accountCelebrationLastFrame==0?0:Math.Clamp(now-accountCelebrationLastFrame,0,.1);
        accountCelebrationLastFrame=now;
        if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
        if(DateTimeOffset.UtcNow>=eventCelebrationNext){
            eventCelebrationNext=DateTimeOffset.UtcNow.AddSeconds(2);
            ObserveEventAccountCompletion();
        }
        if(accountCelebration is null){if(accountCelebrations.Count==0)return;accountCelebration=accountCelebrations.Dequeue();accountCelebrationElapsed=0;}
        var image=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"goddess-complete.png")).GetWrapOrDefault();
        if(image==null)return; // Do not spend the display time while the texture loads.
        accountCelebrationElapsed+=delta;
        if(accountCelebrationElapsed>=AccountCelebrationPolicy.DurationSeconds){
            config.AccountCelebrationsShown??=[];
            if(config.AccountCelebrationsShown.Add(accountCelebration.Value.Key))SaveConfiguration();
            accountCelebration=null;return;
        }
        var alpha=AccountCelebrationPolicy.Alpha(accountCelebrationElapsed);if(alpha<=0)return;
        var content=accountCelebration.Value;
        var viewport=ImGui.GetMainViewport();var center=viewport.Pos+viewport.Size*.5f;
        var scale=Math.Clamp(ImGui.GetFontSize()/17f,.8f,1.6f);
        var draw=ImGui.GetForegroundDrawList();
        uint Color(float r,float g,float b,float a=1)=>ImGui.ColorConvertFloat4ToU32(new(r,g,b,a*alpha));
        var h=210*scale;var w=image==null?h:h*image.Width/image.Height;
        var top=center-new Vector2(w/2,165*scale);
        if(image!=null)draw.AddImage(image.Handle,top,top+new Vector2(w,h),Vector2.Zero,Vector2.One,Color(1,1,1));
        var lines=new[]{"You completed all "+content.Activity+"!",content.Account+" · Good job!"};
        var y=center.Y+58*scale;
        foreach(var line in lines){
            var size=ImGui.CalcTextSize(line);var p=new Vector2(center.X-size.X/2,y);
            draw.AddRectFilled(p-new Vector2(12,5)*scale,p+size+new Vector2(12,5)*scale,Color(.025f,.06f,.12f,.86f),8*scale);
            draw.AddText(p+new Vector2(1,1),Color(0,0,0),line);draw.AddText(p,Color(.79f,.91f,1),line);
            y+=size.Y+12*scale;
        }
    }
}

