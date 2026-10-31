using System.Diagnostics;
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
    private static bool KeyboundSend(nint window,char character)=>PostMessageW(window,0x102,(nint)character,1);
    private static bool KeyboundEnter(nint window)=>PostMessageW(window,0x100,13,0x001c0001)&&PostMessageW(window,0x101,13,unchecked((nint)(int)0xc01c0001));
    private static bool KeyboundUp(nint window)=>PostMessageW(window,0x100,0x26,0x01480001)&&PostMessageW(window,0x101,0x26,unchecked((nint)(int)0xc1480001));
    private void StopKeybound(string reason="Stopped")
    {
        keyboundLastSubmitted=keyboundPendingSubmission="";keyboundRecallTried=keyboundRecalling=keyboundClearing=false;
        keyboundQueueOwned=false;keyboundRegistrarOwned=false;keyboundSubmitAt=default;
        keyboundEnabled=false;keyboundSeen=false;keyboundAwaiting=false;keyboundWord=keyboundSent=keyboundExpected="";keyboundTarget=0;keyboundRecent.Clear();keyboundOpenAttempts=0;keyboundStatus=reason;
    }
    private void DrawKeyboundHelper()
    {
        ImGui.TextUnformatted(keyboundWord.Length>0?keyboundWord:"Keybound Brawler");
        if(ImGui.Button(keyboundEnabled?"Stop":"Start")){
            if(keyboundEnabled)StopKeybound();else{StopKeybound();keyboundEnabled=true;keyboundNext=keyboundHold=keyboundMissing=default;keyboundStatus="Waiting for Keybound Brawler";}
        }
        DrawHelperStatusText(keyboundStatus,stopped:!keyboundEnabled);
        ImGui.Checkbox("Auto-queue & repeat",ref keyboundAutoRepeat);
        ImGui.Checkbox("Anti-AFK while running",ref keyboundAntiAfk);
        var min=Math.Clamp(config.KeyboundMinDelayMs,10,1000);
        var max=Math.Clamp(config.KeyboundMaxDelayMs,min,1000);
        var submit=Math.Clamp(config.KeyboundSubmitDelayMs,0,2000);
        if(ImGui.SliderInt("Min keystroke delay",ref min,10,1000,"%d ms")){config.KeyboundMinDelayMs=min;config.KeyboundMaxDelayMs=Math.Max(min,max);SaveConfiguration();}
        if(ImGui.SliderInt("Max keystroke delay",ref max,min,1000,"%d ms")){config.KeyboundMaxDelayMs=max;SaveConfiguration();}
        if(ImGui.SliderInt("Submit delay",ref submit,0,2000,"%d ms")){config.KeyboundSubmitDelayMs=submit;SaveConfiguration();}
        if(ImGui.CollapsingHeader("How it works")){
            ImGui.TextWrapped("Optional typing assistance for Keybound Brawler. Keeps spaces and punctuation, uses Up for consecutive repeats, and clears its partial text when a target disappears. Pauses through stuns and no-typing casts.");
            ImGui.TextWrapped("Keep this game window in the foreground. Other text fields pause assistance. Auto-queue & repeat registers at the nearby Keybound registrar, accepts the match, and returns after results. Start near the registrar in Keybound Square. Stop or disabling the tool ends automation.");
        }
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
            KeepKeyboundActive(now);
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
            using var process=Process.GetCurrentProcess();var window=process.MainWindowHandle;
            if(window==0||GetForegroundWindow()!=window){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Paused: bring this game window to the foreground";return;}
            var focus=stage->GetFocus();var ownsFocus=editor.Active&&(focus==null||KeyboundContains(&input->UldManager,focus));
            var module=RaptureAtkModule.Instance();if(module==null)return;
            if(!ownsFocus&&module->AtkModule.IsTextInputActive()){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Paused: another text field has focus";return;}
            // Hold throughout these casts, plus their landing animation. No keys,
            // including clearing or Enter, are sent while input is dangerous.
            var dangerName=keyboundDangerName??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(49529)?.Name.ExtractText()??"Cursed Gaze";
            if(KeyboundUi.Texts("KTGEnemyCastBar").Any(t=>t.NodeId==4&&((dangerName.Length>0&&t.Text.Contains(dangerName,StringComparison.OrdinalIgnoreCase))||t.Text.Contains("Sensory Jammer",StringComparison.OrdinalIgnoreCase))))keyboundHold=now.AddMilliseconds(3500);
            if(now<keyboundHold){keyboundDeadline=now.AddSeconds(2);keyboundStatus="Paused: no-typing cast";return;}
            var actual=editor.Text;
            var words=KeyboundUi.AllTexts("KTGEWord").Where(t=>(t.LabelVisible&&t.TextVisible||t.Obscured)&&KeyboundPolicy.IsPhrase(t.Text.Trim())).Select(t=>t with{Text=t.Text.Trim()}).ToArray();
            var live=words.FirstOrDefault(t=>t.Key==keyboundTarget&&t.Text==keyboundWord);
            if(!keyboundClearing&&keyboundWord.Length>0&&keyboundPendingSubmission.Length==0&&KeyboundPolicy.AbandonMissingTarget(live.Text!=null,actual,keyboundSent,keyboundExpected)){
                keyboundAwaiting=keyboundRecalling=false;keyboundSent=actual;keyboundClearing=true;
            }
            if(keyboundAwaiting){
                if(actual==keyboundExpected){keyboundAwaiting=false;keyboundRecalling=false;keyboundSent=actual;keyboundOpenAttempts=0;if(keyboundPendingSubmission.Length>0){keyboundLastSubmitted=keyboundPendingSubmission;keyboundPendingSubmission="";}}
                else {
                    if(now<keyboundDeadline)return;
                    if(keyboundRecalling&&actual.Length==0){keyboundAwaiting=keyboundRecalling=false;keyboundSent="";keyboundStatus="Recall unavailable: typing phrase";}
                    else if(KeyboundPolicy.CanRetryInput(actual,keyboundSent)){
                        // Retry only unchanged owned text or a game-cleared buffer.
                        if(keyboundPendingSubmission.Length>0){keyboundWord=keyboundPendingSubmission;keyboundTarget=0;keyboundPendingSubmission="";}
                        keyboundAwaiting=keyboundRecalling=false;keyboundSent=actual;keyboundNext=now.AddMilliseconds(500);keyboundStatus="Retrying: input was interrupted";return;
                    }
                    else {keyboundStatus="Paused: unexpected input; clear the field to resume";return;}
                }
            }
            if(!KeyboundPolicy.OwnsInput(actual,keyboundSent)){
                if(actual.Length==0){keyboundSent=keyboundExpected="";keyboundPendingSubmission="";}
                else {keyboundStatus="Paused: unexpected text; clear the field to resume";return;}
            }
            if(keyboundWord.Length>0&&live.Text==null){
                if(actual.Length>0){if(!ownsFocus){if(KeyboundEnter(window)){keyboundNext=now.AddMilliseconds(500);keyboundStatus="Reopening input to clear vanished phrase";}return;}if(!KeyboundSend(window,'\b')){StopKeybound("Stopped: input unavailable");return;}KeyboundExpect(actual[..^1],now);keyboundStatus="Clearing vanished phrase";return;}
                keyboundWord="";keyboundTarget=0;keyboundClearing=false;keyboundSubmitAt=default;
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
            if(!keyboundWord.StartsWith(actual,StringComparison.Ordinal)){StopKeybound("Stopped: phrase changed; existing text kept");return;}
            if(actual.Length<keyboundWord.Length){
                var letter=keyboundWord[actual.Length];if(!KeyboundSend(window,letter)){StopKeybound("Stopped: input unavailable");return;}
                KeyboundExpect(actual+letter,now);keyboundNext=now.AddMilliseconds(KeyboundPolicy.TypingDelay(config.KeyboundMinDelayMs,config.KeyboundMaxDelayMs,Random.Shared.NextDouble()));keyboundStatus="Typing: "+keyboundWord;
            }
        }catch(Exception ex){StopKeybound("Stopped: minigame UI unavailable");Log.Warning(ex,"Keybound helper stopped");}
    }
    private void KeyboundExpect(string expected,DateTimeOffset now){keyboundExpected=expected;keyboundAwaiting=true;keyboundDeadline=now.AddSeconds(2);}
}
