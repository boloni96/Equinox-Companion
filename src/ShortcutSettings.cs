using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static readonly string[] ShortcutKeys = ["None", .. Enumerable.Range('A',26).Select(x=>((char)x).ToString()), .. Enumerable.Range(1,24).Select(x=>"F"+x), .. Enumerable.Range(0,10).Select(x=>"Key"+x), "Space", "Tab", "Backspace", "Insert", "Delete", "Home", "End", "PageUp", "PageDown", "LeftArrow", "RightArrow", "UpArrow", "DownArrow", .. Enumerable.Range(0,10).Select(x=>"Keypad"+x)];
    private (string Name, Shortcut Binding)[] Shortcuts() => [("/equinox",config.EquinoxShortcut),("/gardening",config.PlantingShortcut),("/fashionr",config.FashionShortcut),("/cam",config.CameraShortcut)];
    private string? capturingShortcut;
    private ShortcutCapture? shortcutCapture;
    private string shortcutCaptureStatus = "";
    private int shortcutCaptureDrawFrame;
    private readonly ShortcutActivation[] shortcutActivation = [new(), new(), new(), new()];
    private void PauseShortcuts() { foreach (var state in shortcutActivation) state.Reset(); }
    private unsafe void UpdateShortcuts()
    {
        var io=ImGui.GetIO();
        var ui=RaptureAtkModule.Instance();
        var focused=ShortcutKeyboard.OwnGameFocused();
        if(!focused)
        {
            PauseShortcuts();
            if(capturingShortcut is not null)
            {capturingShortcut=null;shortcutCapture=null;shortcutCaptureStatus="Shortcut capture cancelled: game lost focus.";}
            return;
        }
        bool Down(int code)=>ShortcutKeyboard.Down(code,focused);
        var bindings=Shortcuts();
        var ctrl=Down(0x11);var alt=Down(0x12);var shift=Down(0x10);var super=Down(0x5B)||Down(0x5C);
        var blocked=capturingShortcut is not null||!focused
            ||ui==null||ui->IsTextInputActive()||io.WantTextInput||ImGui.IsAnyItemActive();
        var activate=-1;
        for(var i=0;i<bindings.Length;i++)
        {
            var b=bindings[i].Binding;
            if(shortcutActivation[i].Step(b,Down(ShortcutKeyMap.VirtualKey(b.Key)),ctrl,alt,shift,super,blocked)
                &&bindings.Count(x=>x.Binding.Matches(b.Key,b.Ctrl,b.Alt,b.Shift))==1&&activate<0)activate=i;
        }
        if(capturingShortcut is not null)
        {
            if(!visible||!focused||ui==null||ui->IsTextInputActive()||io.WantTextInput||ImGui.GetFrameCount()-shortcutCaptureDrawFrame>1||Down(0x1B))
            {capturingShortcut=null;shortcutCapture=null;shortcutCaptureStatus="Shortcut capture cancelled.";return;}
            var keys=ShortcutKeys.Where(k=>k!="None"&&Down(ShortcutKeyMap.VirtualKey(k))).ToArray();
            var captured=shortcutCapture!.Step(keys,ctrl,alt,shift,super);
            if(captured is not null)
            {
                var name=capturingShortcut;
                if(Shortcuts().Any(s=>s.Name!=name&&s.Binding.Matches(captured.Key,captured.Ctrl,captured.Alt,captured.Shift)))
                    shortcutCaptureStatus="Already assigned to another command. Previous shortcut kept.";
                else
                {
                    var binding=Shortcuts().First(s=>s.Name==name).Binding;
                    binding.Key=captured.Key;binding.Ctrl=captured.Ctrl;binding.Alt=captured.Alt;binding.Shift=captured.Shift;
                    SaveConfiguration();shortcutCaptureStatus=$"Saved {name}: {binding.Label}";
                }
                capturingShortcut=null;shortcutCapture=null;
            }
            return;
        }
        if(activate>=0)
        {
            var name=bindings[activate].Name;
            if(name=="/equinox")OnCommand(name,"");
            else if(name=="/gardening")OnPlantingCommand(name,"");
            else if(name=="/cam")SetCameraHeightEnabled(!config.RememberCameraHeight);
            else OnFashionCommand(name,"");
        }
    }
    private void DrawShortcutSettings()
    {
        shortcutCaptureDrawFrame=ImGui.GetFrameCount();
        SettingsHelp("Click Set shortcut, press your combination, then release all keys to save. Esc cancels. Use one key with optional Ctrl, Alt or Shift.",false);
        foreach(var (name,binding) in Shortcuts())
        {
            ImGui.PushID(name);ImGui.TextUnformatted(name+" — "+binding.Label);ImGui.SameLine();
            if(capturingShortcut==name)
            {
                ImGui.TextWrapped(shortcutCapture?.Pending is {} pending?$"{pending.Label} — release to save":"Listening… press your shortcut (Esc to cancel).");
                if(!string.IsNullOrEmpty(shortcutCapture?.Error))ImGui.TextWrapped(shortcutCapture.Error);
                if(ImGui.Button("Cancel")){capturingShortcut=null;shortcutCapture=null;shortcutCaptureStatus="Shortcut capture cancelled.";}
            }
            else if(ImGui.Button("Set shortcut")){capturingShortcut=name;shortcutCapture=new();shortcutCaptureStatus="";}
            ImGui.SameLine();
            if(ImGui.Button("Clear"))
            {if(capturingShortcut==name){capturingShortcut=null;shortcutCapture=null;}binding.Key="None";binding.Ctrl=binding.Alt=binding.Shift=false;SaveConfiguration();shortcutCaptureStatus=name+" shortcut cleared.";}
            if(binding.Key!="None"&&Shortcuts().Count(x=>x.Binding.Matches(binding.Key,binding.Ctrl,binding.Alt,binding.Shift))>1)ImGui.TextWrapped("Duplicate shortcut: assign a different combination. Conflicting shortcuts are disabled.");
            ImGui.PopID();
        }
        if(shortcutCaptureStatus.Length>0)ImGui.TextWrapped(shortcutCaptureStatus);
        SettingsHelp("Shortcuts pause while typing or editing a control and while the game is unfocused. They do not replace FFXIV keybinds: choose unused combinations. /gardening still requires an identified paired house.");
    }
    private void MessageToggle(string label,bool value,Action<bool> set)
    { if(ImGui.Checkbox(label,ref value)){set(value);SaveConfiguration();} }
    private void DrawChatMessageSettings()
    {
        SettingsHelp("Choose which Equinox messages appear in your own chat. These controls do not disable tracking or syncing.",false);
        ImGui.Separator();ImGui.TextUnformatted("Housing");
        MessageToggle("House entry messages",config.NotifyHouseEntries,v=>config.NotifyHouseEntries=v);
        MessageToggle("Housing reminders after 30 days",config.NotifyHousingWarnings,v=>config.NotifyHousingWarnings=v);
        MessageToggle("Housing lottery period · once after login",config.NotifyHousingLottery,v=>config.NotifyHousingLottery=v);
        ImGui.Separator();ImGui.TextUnformatted("Gardening");
        MessageToggle("Garden chat messages enabled",config.NotifyGardenCare,v=>config.NotifyGardenCare=v);
        ImGui.BeginDisabled(!config.NotifyGardenCare);
        MessageToggle("Persons needing to tend (light blue)",config.NotifyGardenTending,v=>config.NotifyGardenTending=v);
        MessageToggle("Persons with gardens at risk (orange)",config.NotifyGardenRisk,v=>config.NotifyGardenRisk=v);
        ImGui.EndDisabled();
        SettingsHelp("Once per character login: one light-blue tending message and/or one orange risk message, grouped by Person across all paired accounts and characters. Mature crops do not trigger reminders. No house lists or repeated messages during the session.");
        ImGui.Separator();ImGui.TextUnformatted("Maintenance & command messages");
        MessageToggle("Maintenance reminder · 5 minutes after login (local computer time)",config.NotifyMaintenance,v=>config.NotifyMaintenance=v);
        MessageToggle("Fashion Report browser link",config.NotifyFashionLink,v=>config.NotifyFashionLink=v);
        MessageToggle("Gardening command: house not identified",config.NotifyPlantingUnavailable,v=>config.NotifyPlantingUnavailable=v);
        MessageToggle("Fashion Report: could not open browser",config.NotifyBrowserErrors,v=>config.NotifyBrowserErrors=v);
    }
}
