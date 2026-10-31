using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using EquinoxCompanion.Keybound;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private bool keyboundEnabled,keyboundSeen,keyboundAwaiting;
    private bool keyboundAutoRepeat;
    private int keyboundDelayDraft;
    private DateTimeOffset keyboundTimingEditUntil;
    private DateTimeOffset keyboundSubmitAt;
    private string keyboundStatus="Off",keyboundWord="",keyboundSent="",keyboundExpected="";
    private nint keyboundTarget;
    private string keyboundLastSubmitted="",keyboundPendingSubmission="";
    private bool keyboundRecallTried,keyboundRecalling,keyboundClearing;
    private DateTimeOffset keyboundNext,keyboundDeadline,keyboundHold,keyboundMissing;
    private int keyboundOpenAttempts;
    private string? keyboundDangerName;
    private readonly Dictionary<string,DateTimeOffset> keyboundRecent=[];
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll",SetLastError=true)] private static extern bool PostMessageW(nint window,uint message,nint value,nint detail);
    private void StopKeybound(string reason="Stopped")
    {
        keyboundLastSubmitted=keyboundPendingSubmission="";keyboundRecallTried=keyboundRecalling=keyboundClearing=false;
        keyboundQueueOwned=false;keyboundRegistrarOwned=false;keyboundSubmitAt=default;
        keyboundEnabled=false;keyboundSeen=false;keyboundAwaiting=false;keyboundWord=keyboundSent=keyboundExpected="";keyboundTarget=0;keyboundRecent.Clear();keyboundOpenAttempts=0;keyboundStatus=reason;
    }
    private void DrawKeyboundHelper()
    {
        ImGui.BeginDisabled(keyboundBuying);
        if(ImGui.Button(keyboundEnabled?"Stop":"Start")){
            if(keyboundEnabled)StopKeybound();else{StopKeybound();keyboundEnabled=true;keyboundNext=keyboundHold=keyboundMissing=default;keyboundStatus="Waiting for Keybound Brawler";}
        }
        ImGui.EndDisabled();
        ImGui.SameLine();ImGui.TextUnformatted(keyboundEnabled?"Running":"Stopped");
        ImGui.TextWrapped(keyboundStatus);
        if(keyboundWord.Length>0)ImGui.TextWrapped("Word: "+keyboundWord);
        DrawKeyboundRewards();
        DrawKeyboundShopping();
        if(ImGui.CollapsingHeader("Options")){
        ImGui.Checkbox("Auto-repeat",ref keyboundAutoRepeat);
        ImGui.SameLine();ImGui.Checkbox("Anti-AFK",ref keyboundAntiAfk);
        var background=config.KeyboundRunInBackground;
        if(ImGui.Checkbox("Run in background",ref background)){config.KeyboundRunInBackground=background;SaveConfiguration();}
        if(ImGui.Button("Soft · 80 / 150 / 80 ms")){
            config.KeyboundMinDelayMs=KeyboundPolicy.SoftMinMs;config.KeyboundMaxDelayMs=KeyboundPolicy.SoftMaxMs;config.KeyboundSubmitDelayMs=KeyboundPolicy.SoftSubmitMs;SaveConfiguration();
        }
        var min=Math.Clamp(config.KeyboundMinDelayMs,10,1000);
        var max=Math.Clamp(config.KeyboundMaxDelayMs,10,1000);
        var submit=Math.Clamp(config.KeyboundSubmitDelayMs,0,2000);
        if(DrawKeyboundDelay("Min",ref min,10,1000)){config.KeyboundMinDelayMs=min;SaveConfiguration();}
        if(DrawKeyboundDelay("Max",ref max,10,1000)){config.KeyboundMaxDelayMs=max;SaveConfiguration();}
        if(DrawKeyboundDelay("Submit",ref submit,0,2000)){config.KeyboundSubmitDelayMs=submit;SaveConfiguration();}
        if(min>max)ImGui.TextDisabled($"Effective keystroke range: {max}–{min} ms");
        ImGui.BeginDisabled(keyboundBuying);
        var journal=config.KeyboundUseJournalWishlist;
        if(ImGui.Checkbox("Use Journal wishlist",ref journal)){config.KeyboundUseJournalWishlist=journal;keyboundBuyRefresh=default;SaveConfiguration();}
        ImGui.EndDisabled();
        }
        if(ImGui.CollapsingHeader("How it works")){
            ImGui.TextWrapped("Optional typing assistance for Keybound Brawler. Keeps spaces and punctuation, uses Up for consecutive repeats, and clears its partial text when a target disappears. Pauses through stuns and no-typing casts.");
            ImGui.TextWrapped("Run in background keeps working while you use another app. Input goes directly to this game client. Keep the game running; background frame limits can slow it down. Other in-game text fields pause assistance. Auto-queue & repeat registers at the nearby Keybound registrar, accepts the match, and returns after results. Start near the registrar in Keybound Square. Stop or disabling the tool ends automation.");
        }
    }
    private bool DrawKeyboundDelay(string label,ref int value,int minimum,int maximum)
    {
        ImGui.SetNextItemWidth(Math.Max(120,ImGui.GetContentRegionAvail().X-ImGui.GetFontSize()*4.5f));
        var changed=ImGui.SliderInt(label,ref value,minimum,maximum,"%d ms");
        if(ImGui.IsItemActive())keyboundTimingEditUntil=DateTimeOffset.UtcNow.AddMilliseconds(250);
        var popup="Exact milliseconds##"+label;
        if(ImGui.IsItemClicked(ImGuiMouseButton.Right)){keyboundDelayDraft=value;ImGui.OpenPopup(popup);}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Drag to adjust · right-click to type milliseconds");
        if(ImGui.BeginPopup(popup)){
            keyboundTimingEditUntil=DateTimeOffset.UtcNow.AddMilliseconds(250);
            ImGui.TextUnformatted(label);
            if(ImGui.IsWindowAppearing())ImGui.SetKeyboardFocusHere();
            ImGui.SetNextItemWidth(160);
            var enter=ImGui.InputInt("Milliseconds",ref keyboundDelayDraft,0,0,"%d",ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.TextDisabled($"{minimum}–{maximum} ms");
            if(ImGui.Button("Apply")||enter){value=Math.Clamp(keyboundDelayDraft,minimum,maximum);changed=true;ImGui.CloseCurrentPopup();}
            ImGui.SameLine();if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        return changed;
    }
    private static unsafe bool KeyboundContains(AtkUldManager* uld,AtkResNode* target,int depth=0)
    {
        if(uld==null||uld->NodeList==null||depth>8)return false;
        for(int i=0;i<Math.Min((int)uld->NodeListCount,1024);i++){
            var node=uld->NodeList[i];if(node==null)continue;if(node==target)return true;
            if((int)node->Type>=1000){var component=node->GetComponent();if(component!=null&&KeyboundContains(&component->UldManager,target,depth+1))return true;}
        }
        return false;
    }
    // Unknown input is not an empty buffer. Use the native component's active state.
    private static unsafe (string? Text,bool Active) ReadKeyboundInput(AtkUnitBase* addon)
    {
        if(addon==null||!addon->IsReady||addon->UldManager.NodeList==null)return (null,false);
        for(var i=0;i<Math.Min((int)addon->UldManager.NodeListCount,1024);i++){
            var node=addon->UldManager.NodeList[i];
            if(node==null||node->NodeId!=7||(int)node->Type<1000)continue;
            var component=node->GetComponent();
            if(component==null||component->GetComponentType()!=ComponentType.TextInput)return (null,false);
            var field=(AtkComponentTextInput*)component;
            var uld=&component->UldManager;if(uld->NodeList==null)return (null,field->IsActive);
            for(var j=0;j<Math.Min((int)uld->NodeListCount,1024);j++){
                var child=uld->NodeList[j];
                if(child!=null&&child->NodeId==16&&child->Type==NodeType.Text)
                    return (((AtkTextNode*)child)->NodeText.ToString(),field->IsActive);
            }
        }
        return (null,false);
    }
    private unsafe void UpdateKeybound(IFramework framework)
    {
        if(!keyboundEnabled)return;
        if(!config.EnableKeyboundBrawler){StopKeybound("Stopped: tool disabled");return;}
        var now=DateTimeOffset.UtcNow;if(now<keyboundNext)return;keyboundNext=now.AddMilliseconds(10);
        try{
            if(!Client.IsLoggedIn){StopKeybound("Stopped: logged out");return;}
            if(now<keyboundTimingEditUntil){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Paused: editing timing";return;}
            var stage=AtkStage.Instance();if(stage==null)return;
            if(keyboundAutoRepeat&&UpdateKeyboundLoop(now))return;
            var input=stage->RaptureAtkUnitManager->GetAddonByName("KTGInput");
            if(input==null||!input->IsVisible){
                if(Client.TerritoryType==1359){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Waiting: minigame transition";keyboundNext=now.AddMilliseconds(500);return;}
                if(keyboundSeen){if(keyboundMissing==default)keyboundMissing=now;if(now-keyboundMissing>TimeSpan.FromSeconds(2))StopKeybound("Stopped: minigame closed");}
                else{keyboundStatus="Waiting for Keybound Brawler";keyboundNext=now.AddMilliseconds(500);}return;
            }
            keyboundSeen=true;keyboundMissing=default;
            var editor=ReadKeyboundInput(input);
            if(editor.Text is null){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Waiting: minigame input is loading";return;}
            if(Objects.LocalPlayer?.StatusList.Any(status=>status.StatusId is 2 or 149 or 1309)==true){
                keyboundDeadline=now.AddSeconds(2);keyboundStatus="Paused: stunned; resumes automatically";return;
            }
            var window=KeyboundGameWindow();
            if(window==0){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Waiting for game input";return;}
            if(!config.KeyboundRunInBackground&&GetForegroundWindow()!=window){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Paused: enable Run in background or return to the game";return;}
            var focus=stage->GetFocus();var ownsFocus=editor.Active&&(focus==null||KeyboundContains(&input->UldManager,focus));
            var module=RaptureAtkModule.Instance();if(module==null)return;
            if(!ownsFocus&&module->AtkModule.IsTextInputActive()){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Paused: another text field has focus";return;}
            // Hold throughout these casts, plus their landing animation. No keys,
            // including clearing or Enter, are sent while input is dangerous.
            var dangerName=keyboundDangerName??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(49529)?.Name.ExtractText()??"Cursed Gaze";
            if(KeyboundUi.Texts("KTGEnemyCastBar").Any(t=>t.NodeId==4&&((dangerName.Length>0&&t.Text.Contains(dangerName,StringComparison.OrdinalIgnoreCase))||t.Text.Contains("Sensory Jammer",StringComparison.OrdinalIgnoreCase))))keyboundHold=now.AddMilliseconds(3500);
            if(now<keyboundHold){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Paused: no-typing cast";return;}
            var actual=editor.Text;
            var words=KeyboundUi.AllTexts("KTGEWord").Where(t=>KeyboundPolicy.LiveWord(t.ContainerVisible,t.LabelVisible,t.TextVisible,t.Obscured)&&KeyboundPolicy.IsPhrase(t.Text.Trim())).Select(t=>t with{Text=t.Text.Trim()}).ToArray();
            var live=words.FirstOrDefault(t=>t.Key==keyboundTarget&&t.Text==keyboundWord);
            if(!keyboundClearing&&keyboundWord.Length>0&&keyboundPendingSubmission.Length==0&&KeyboundPolicy.AbandonMissingTarget(live.Text!=null,actual,keyboundSent,keyboundExpected)){
                keyboundAwaiting=keyboundRecalling=false;keyboundSent=actual;keyboundClearing=true;
            }
            if(keyboundAwaiting){
                if(actual==keyboundExpected){keyboundAwaiting=false;keyboundRecalling=false;keyboundSent=actual;keyboundOpenAttempts=0;if(keyboundPendingSubmission.Length>0){keyboundLastSubmitted=keyboundPendingSubmission;keyboundPendingSubmission="";}}
                else {
                    if(now<keyboundDeadline)return;
                    if(keyboundRecalling&&actual.Length==0){keyboundAwaiting=keyboundRecalling=false;keyboundSent="";keyboundStatus="Recall unavailable: typing phrase";}
                    else if(KeyboundPolicy.CanRecoverInput(actual,keyboundSent,keyboundExpected,keyboundAwaiting)){
                        BeginKeyboundRecovery(actual);
                    }
                    else {keyboundStatus="Paused: another input changed the field; clear it to resume";return;}
                }
            }
            if(!KeyboundPolicy.OwnsInput(actual,keyboundSent)){
                if(actual.Length==0){keyboundSent=keyboundExpected="";keyboundPendingSubmission="";}
                else if(KeyboundPolicy.CanRecoverInput(actual,keyboundSent,keyboundExpected,keyboundAwaiting)){BeginKeyboundRecovery(actual);}
                else {keyboundStatus="Paused: another input changed the field; clear it to resume";return;}
            }
            if(keyboundClearing||(keyboundWord.Length>0&&live.Text==null)){
                if(actual.Length>0){if(!ownsFocus){if(KeyboundEnter(window)){keyboundNext=now.AddMilliseconds(500);keyboundStatus="Reopening input to clear vanished phrase";}return;}if(!KeyboundPolicy.CanClear(ownsFocus,actual,keyboundSent))return;if(!KeyboundClear(window)){StopKeybound("Stopped: input unavailable");return;}KeyboundExpect("",now);keyboundStatus="Clearing interrupted phrase";return;}
                keyboundWord=keyboundSent=keyboundExpected="";keyboundTarget=0;keyboundClearing=false;keyboundSubmitAt=default;
            }
            if(keyboundWord.Length==0){
                foreach(var old in keyboundRecent.Where(x=>now-x.Value>TimeSpan.FromSeconds(2)).Select(x=>x.Key).ToArray())keyboundRecent.Remove(old);
                var next=words.Where(t=>!keyboundRecent.ContainsKey(t.Text)).OrderBy(t=>t.Tint==LabelTint.Green?0:t.Tint==LabelTint.Red?1:t.Tint==LabelTint.Gold?2:3).ThenByDescending(t=>t.Y).FirstOrDefault();
                if(next.Text==null){keyboundStatus="Waiting for a live phrase";return;}keyboundWord=next.Text;keyboundTarget=next.Key;keyboundRecallTried=keyboundClearing=false;keyboundSubmitAt=default;live=next;
            }
            if(!ownsFocus){
                if(++keyboundOpenAttempts>3){keyboundOpenAttempts=0;keyboundNext=now.AddSeconds(1);keyboundStatus="Waiting: minigame input is not active yet";return;}
                if(!KeyboundEnter(window)){StopKeybound("Stopped: input unavailable");return;}keyboundNext=now.AddMilliseconds(300);keyboundStatus="Opening minigame input";return;
            }
            if(KeyboundPolicy.CanSubmit(live.Text??"",actual,keyboundWord)){
                if(keyboundSubmitAt==default)keyboundSubmitAt=now.AddMilliseconds(Math.Clamp(config.KeyboundSubmitDelayMs,0,2000));
                if(now<keyboundSubmitAt)return;
                if(!KeyboundEnter(window)){StopKeybound("Stopped: submit unavailable");return;}
                keyboundPendingSubmission=keyboundWord;keyboundRecent[keyboundWord]=now;keyboundStatus="Submitted: "+keyboundWord;keyboundWord="";keyboundTarget=0;KeyboundExpect("",now);return;
            }
            if(KeyboundPolicy.ShouldRecall(actual,keyboundWord,keyboundLastSubmitted,keyboundRecallTried)){
                keyboundRecallTried=true;
                if(!KeyboundUp(window)){StopKeybound("Stopped: recall unavailable");return;}
                KeyboundExpect(keyboundWord,now);keyboundRecalling=true;keyboundStatus="Recalling: "+keyboundWord;return;
            }
            if(!keyboundWord.StartsWith(actual,StringComparison.Ordinal)){BeginKeyboundRecovery(actual);return;}
            if(actual.Length<keyboundWord.Length){
                var letter=keyboundWord[actual.Length];if(!KeyboundSend(window,letter)){StopKeybound("Stopped: input unavailable");return;}
                KeyboundExpect(actual+letter,now);keyboundNext=now.AddMilliseconds(KeyboundPolicy.TypingDelay(config.KeyboundMinDelayMs,config.KeyboundMaxDelayMs,Random.Shared.NextDouble()));keyboundStatus="Typing: "+keyboundWord;
            }
        }catch(Exception ex){StopKeybound("Stopped: minigame UI unavailable");Log.Warning(ex,"Keybound helper stopped");}
    }
    private void BeginKeyboundRecovery(string actual)
    {
        // Called only after ownership was established. Drain the owned field
        // before choosing another target; never use Escape on an empty field.
        keyboundSent=actual;keyboundExpected="";keyboundAwaiting=keyboundRecalling=false;
        keyboundPendingSubmission="";keyboundClearing=true;keyboundSubmitAt=default;
        keyboundStatus="Recovering interrupted phrase";
    }
    private void KeyboundExpect(string expected,DateTimeOffset now){keyboundExpected=expected;keyboundAwaiting=true;keyboundDeadline=now.AddSeconds(2);}
}

