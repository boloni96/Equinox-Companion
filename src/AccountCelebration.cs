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
    private string accountCelebrationPairing="";
    private static string CelebrationKey(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private string CelebrationImageKey(string identity)=>CelebrationKey(config.PairingKey+"|"+identity+"|image-size-2");
    private void ResetCelebrationScope()
    {
        if(accountCelebrationPairing==config.PairingKey)return;
        accountCelebrationPairing=config.PairingKey;accountCelebrationObserved.Clear();accountCelebrations.Clear();accountCelebration=null;completedEventCelebration=null;eventCelebrationStatus="Waiting for account completion data.";RequestCelebrationCheck();
    }
    private void QueueAccountCelebration(string identity,string account,string activity,bool replay=false)
    {
        ResetCelebrationScope();
        var messageKey=CelebrationKey(config.PairingKey+"|"+identity);
        var key=CelebrationImageKey(identity);
        config.AccountCelebrationsShown??=[];
        config.AccountCompletionMessagesShown??=[];
        if(!replay&&!config.AccountCompletionMessagesShown.Contains(messageKey)){
            PrintEquinox(AccountCelebrationPolicy.CompletionMessage(account,activity));
            config.AccountCompletionMessagesShown=new(config.AccountCompletionMessagesShown){messageKey};SaveConfiguration();
        }
        if(!replay&&config.AccountCelebrationsShown.Contains(key)||accountCelebration?.Key==key||accountCelebrations.Any(c=>c.Key==key))return;
        accountCelebrations.Enqueue((key,account,activity));
    }
    private void ObserveAccountFashionCompletion()
    {
        if(!Player.IsLoaded)return;
        ResetCelebrationScope();
        var now=DateTimeOffset.UtcNow;if(!FashionCompletion.IsOpen(now))return;
        var current=CurrentSharedCharacter();if(current==null||string.IsNullOrEmpty(current.AccountId))return;
        var person=(config.SharedRoster?.People??[]).SingleOrDefault(p=>p.Characters.Any(c=>c.Id==current.Id));
        if(person==null)return;
        var characters=person.Characters.Where(c=>c.AccountId==current.AccountId).DistinctBy(c=>c.Id).ToArray();
        var tasks=adventureBoards.SelectMany(b=>b.Stops).SelectMany(s=>s.Tasks)
            .Where(t=>AdventurePolicy.IsFashion(t)&&AdventurePolicy.TaskCharacterId(t,characters).Length>0).ToArray();
        // A saved Adventure defines the participating Fashion characters. Without
        // one, require completion for every character linked to this account.
        var participants=tasks.Select(t=>AdventurePolicy.TaskCharacterId(t,characters)).ToHashSet();
        if(participants.Count>0)characters=characters.Where(c=>participants.Contains(c.Id)).ToArray();
        if(characters.Length==0)return;
        var identity=$"fashion-account-v3|{person.Id}|{current.AccountId}|{FashionCompletion.Cycle(now):O}|"+string.Join(",",characters.Select(c=>c.Id).Order());
        if(config.AccountCelebrationsShown?.Contains(CelebrationImageKey(identity))==true)return;
        var observations=DiscoveryReads().Kind("fashion.observed").ToArray();
        foreach(var c in characters){
            var actor=new Actor(c.ContentId,c.Name,0,0,c.World,c.World);
            if(!FashionCompletion.IsComplete(actor,observations,[person],now))return;
        }
        QueueAccountCelebration(identity,person.Name+" / "+current.Account,$"Fashion Report (week of {FashionCompletion.Cycle(now):yyyy-MM-dd})");
    }
    private void CelebrateEventAccount()=>RequestCelebrationCheck();
    private IEnumerable<bool> ObserveEventAccountCompletion(long evidenceRevision)
    {
        completedEventCelebration=null;
        eventCelebrationStatus="Waiting for a linked character and account.";
        if(!Player.IsLoaded)yield break;
        if(keyboundBuying){eventCelebrationStatus="Waiting for buying / reward use to finish.";yield break;}
        var current=CurrentSharedCharacter();if(current==null||string.IsNullOrEmpty(current.AccountId))yield break;
        var person=(config.SharedRoster?.People??[]).SingleOrDefault(p=>p.Characters.Any(c=>c.Id==current.Id));
        if(person==null)yield break;
        // Wanted rewards define the account's event participants, independently of
        // which characters were selected for an automatic routine.
        var characters=person.Characters.Where(c=>c.AccountId==current.AccountId&&c.KeyboundWishlist?.Any(w=>w.Wanted)==true).DistinctBy(c=>c.Id).ToArray();
        if(characters.Length==0){eventCelebrationStatus="No wanted event rewards are selected for this account.";yield break;}
        var identity=$"keybound-5466-wishlist-v3|{person.Id}|{current.AccountId}|"+string.Join(",",characters.Select(c=>c.Id).Order());
        var tasks=adventureBoards.SelectMany(b=>b.Stops).SelectMany(s=>s.Tasks).Where(t=>t.Rule?.Kind=="event"&&t.Rule.Quests?.Any(q=>QuestToolsPolicy.ShortId(q)==QuestToolsPolicy.KeywardBound)==true).ToArray();
        {
            foreach(var step in PrepareCelebrationOffers())yield return step;
            keyboundPurchases??=new(Path.Combine(Pi.GetPluginConfigDirectory(),"keybound-purchases.json"));
            var loadStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            try{keyboundPurchases!.Reload();}
            finally{RecordPerformanceElapsed("Celebration: purchase history",loadStarted);}
            yield return true;
            config.EventCelebrationEvidence??=[];
            var pending=new List<string>();var done=0;
            foreach(var c in characters){
                AccountEventEvidence? evidence=null;
                foreach(var step in ReadEventCelebrationEvidence(c,current,tasks,value=>evidence=value))yield return step;
                if(evidence==null)yield break;
                var missing=c.KeyboundWishlist!.Where(w=>w.Wanted&&!AccountCelebrationPolicy.RewardComplete(w.Obtained,
                    celebrationOffers!.Any(o=>o.Key==KeyboundShoppingPolicy.PrizeKey(w.Name)&&evidence.Items.Contains(o.Item)))).ToArray();
                if(!evidence.QuestComplete)pending.Add(c.Name+": event quest completion not confirmed");
                if(missing.Length>0)pending.Add(c.Name+": "+missing.Length+" reward(s) not confirmed ("+string.Join(", ",missing.Take(2).Select(w=>w.Name))+")");
                if(evidence.QuestComplete&&missing.Length==0)done++;
            }
            eventCelebrationStatus=$"{current.Account}: {done}/{characters.Length} event characters complete.";
            if(pending.Count>0){eventCelebrationStatus+="\n"+string.Join("\n",pending.Take(6));if(pending.Count>6)eventCelebrationStatus+="\nMore characters still need confirmation.";yield break;}
        }
        if(evidenceRevision!=celebrationEvidenceRevision)yield break; // New evidence will run next; never celebrate an obsolete wishlist.
        completedEventCelebration=(identity,person.Name+" / "+current.Account);
        eventCelebrationStatus+=" Good Job is ready; it waits until this game client is in front. You can replay it below.";
        // Catch up once when the first synchronized observation is already complete.
        // The durable shown key prevents repeating this on login or every poll.
        QueueAccountCelebration(identity,person.Name+" / "+current.Account,"Keybound Brawler + reward list");
    }
    private void DrawAccountCelebration()
    {
        // Foreground drawing has no window, focus, input capture, or saved size.
        ResetCelebrationScope();
        var now=ImGui.GetTime();
        var delta=accountCelebrationLastFrame==0?0:Math.Clamp(now-accountCelebrationLastFrame,0,.1);
        accountCelebrationLastFrame=now;
        if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
        if(accountCelebration is null){if(accountCelebrations.Count==0)return;accountCelebration=accountCelebrations.Dequeue();accountCelebrationElapsed=0;}
        var image=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"goddess-complete.png")).GetWrapOrDefault();
        if(image==null)return; // Do not spend the display time while the texture loads.
        var focused=ShortcutKeyboard.OwnGameFocused();
        accountCelebrationElapsed+=AccountCelebrationPolicy.VisibleDelta(delta,focused,true,image!=null);
        if(!focused)return; // A background client must not consume the celebration.

        if(accountCelebrationElapsed>=AccountCelebrationPolicy.DurationSeconds){
            config.AccountCelebrationsShown??=[];
            if(!config.AccountCelebrationsShown.Contains(accountCelebration.Value.Key)){config.AccountCelebrationsShown=new(config.AccountCelebrationsShown){accountCelebration.Value.Key};SaveConfiguration();}
            accountCelebration=null;return;
        }
        var alpha=AccountCelebrationPolicy.Alpha(accountCelebrationElapsed);if(alpha<=0)return;
        var content=accountCelebration.Value;
        var viewport=ImGui.GetMainViewport();var center=viewport.Pos+viewport.Size*.5f;
        var scale=Math.Clamp(ImGui.GetFontSize()/17f,.8f,1.6f);
        var draw=ImGui.GetForegroundDrawList();
        uint Color(float r,float g,float b,float a=1)=>ImGui.ColorConvertFloat4ToU32(new(r,g,b,a*alpha));
        var h=420*scale;var w=image==null?h:h*image.Width/image.Height;
        var top=center-new Vector2(w/2,h/2+30*scale);
        if(image!=null)draw.AddImage(image.Handle,top,top+new Vector2(w,h),Vector2.Zero,Vector2.One,Color(1,1,1));
        var lines=new[]{"You completed all "+content.Activity+"!",content.Account+" · Good job!"};
        var textScale=2f;
        var widest=lines.Max(line=>ImGui.CalcTextSize(line).X);
        if(widest>0)textScale=Math.Min(textScale,Math.Max(1f,(viewport.Size.X-48*scale)/widest));
        var fontSize=ImGui.GetFontSize()*textScale;
        var textHeight=lines.Sum(line=>ImGui.CalcTextSize(line).Y*textScale+12*scale);
        var y=top.Y-textHeight-12*scale;
        foreach(var line in lines){
            var size=ImGui.CalcTextSize(line)*textScale;var p=new Vector2(center.X-size.X/2,y);
            draw.AddRectFilled(p-new Vector2(12,5)*scale,p+size+new Vector2(12,5)*scale,Color(.025f,.06f,.12f,.86f),8*scale);
            draw.AddText(ImGui.GetFont(),fontSize,p+new Vector2(1,1),Color(0,0,0),line);draw.AddText(ImGui.GetFont(),fontSize,p,Color(.79f,.91f,1),line);
            y+=size.Y+12*scale;
        }
    }
}

