using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static readonly string[] ShortcutKeys = ["None", .. Enumerable.Range('A',26).Select(x=>((char)x).ToString()), .. Enumerable.Range(1,24).Select(x=>"F"+x), .. Enumerable.Range(0,10).Select(x=>"Key"+x), "Space", "Tab", "Backspace", "Insert", "Delete", "Home", "End", "PageUp", "PageDown", "LeftArrow", "RightArrow", "UpArrow", "DownArrow", .. Enumerable.Range(0,10).Select(x=>"Keypad"+x)];
    private (string Name, Shortcut Binding)[] Shortcuts() => [("/equinox",config.EquinoxShortcut),("/planting",config.PlantingShortcut),("/fashionr",config.FashionShortcut)];
    private string? capturingShortcut;
    private ShortcutCapture? shortcutCapture;
    private string shortcutCaptureStatus = "";
    private int shortcutCaptureDrawFrame;
    private unsafe void UpdateShortcuts()
    {
        var io=ImGui.GetIO();
        var game=FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        var ui=RaptureAtkModule.Instance();
        if(capturingShortcut is not null)
        {
            if(!visible||game==null||!game->CursorInputs.IsGameWindowFocused||ui==null||ui->IsTextInputActive()||io.WantTextInput||ImGui.GetFrameCount()-shortcutCaptureDrawFrame>1||ImGui.IsKeyPressed(ImGuiKey.Escape,false))
            {capturingShortcut=null;shortcutCapture=null;shortcutCaptureStatus="Shortcut capture cancelled.";return;}
            var keys=ShortcutKeys.Where(k=>k!="None"&&Enum.TryParse<ImGuiKey>(k,out var code)&&ImGui.IsKeyDown(code)).ToArray();
            var captured=shortcutCapture!.Step(keys,io.KeyCtrl,io.KeyAlt,io.KeyShift,io.KeySuper);
            if(captured is not null)
            {
                var name=capturingShortcut;
                if(Shortcuts().Any(s=>s.Name!=name&&s.Binding.Matches(captured.Key,captured.Ctrl,captured.Alt,captured.Shift)))
                    shortcutCaptureStatus="Already assigned to another command. Previous shortcut kept.";
                else
                {
                    var binding=Shortcuts().First(s=>s.Name==name).Binding;
                    binding.Key=captured.Key;binding.Ctrl=captured.Ctrl;binding.Alt=captured.Alt;binding.Shift=captured.Shift;
                    Pi.SavePluginConfig(config);shortcutCaptureStatus=$"Saved {name}: {binding.Label}";
                }
                capturingShortcut=null;shortcutCapture=null;
            }
            return;
        }
        if(game==null||!game->CursorInputs.IsGameWindowFocused||ui==null||ui->IsTextInputActive()||io.WantTextInput||ImGui.IsAnyItemActive())return;
        var bindings=Shortcuts();
        foreach(var (name,binding) in bindings)
        {
            if(binding.Key=="None"||!ShortcutKeys.Contains(binding.Key)||!Enum.TryParse<ImGuiKey>(binding.Key,out var key))continue;
            if(!binding.Matches(binding.Key,io.KeyCtrl,io.KeyAlt,io.KeyShift)||io.KeySuper||!ImGui.IsKeyPressed(key,false))continue;
            // Duplicate shortcuts do nothing; the settings screen highlights the conflict.
            if(bindings.Count(x=>x.Binding.Matches(binding.Key,binding.Ctrl,binding.Alt,binding.Shift))!=1)continue;
            if(name=="/equinox")OnCommand(name,"");
            else if(name=="/planting")OnPlantingCommand(name,"");
            else OnFashionCommand(name,"");
            break;
        }
    }
    private void DrawShortcutSettings()
    {
        shortcutCaptureDrawFrame=ImGui.GetFrameCount();
        ImGui.TextWrapped("Click Set shortcut, press your combination, then release all keys to save. Esc cancels. Use one key with optional Ctrl, Alt or Shift.");
        foreach(var (name,binding) in Shortcuts())
        {
            ImGui.PushID(name);ImGui.Separator();ImGui.TextUnformatted(name+" — "+binding.Label);
            if(capturingShortcut==name)
            {
                ImGui.TextWrapped(shortcutCapture?.Pending is {} pending?$"{pending.Label} — release to save":"Listening… press your shortcut (Esc to cancel).");
                if(!string.IsNullOrEmpty(shortcutCapture?.Error))ImGui.TextWrapped(shortcutCapture.Error);
                if(ImGui.Button("Cancel")){capturingShortcut=null;shortcutCapture=null;shortcutCaptureStatus="Shortcut capture cancelled.";}
            }
            else if(ImGui.Button("Set shortcut")){capturingShortcut=name;shortcutCapture=new();shortcutCaptureStatus="";}
            ImGui.SameLine();
            if(ImGui.Button("Clear"))
            {if(capturingShortcut==name){capturingShortcut=null;shortcutCapture=null;}binding.Key="None";binding.Ctrl=binding.Alt=binding.Shift=false;Pi.SavePluginConfig(config);shortcutCaptureStatus=name+" shortcut cleared.";}
            if(binding.Key!="None"&&Shortcuts().Count(x=>x.Binding.Matches(binding.Key,binding.Ctrl,binding.Alt,binding.Shift))>1)ImGui.TextWrapped("Duplicate shortcut: assign a different combination. Conflicting shortcuts are disabled.");
            ImGui.PopID();
        }
        if(shortcutCaptureStatus.Length>0)ImGui.TextWrapped(shortcutCaptureStatus);
        ImGui.TextWrapped("Shortcuts pause while typing or editing a control and while the game is unfocused. They do not replace FFXIV keybinds: choose unused combinations. /planting still requires an identified paired house.");
    }
    private void MessageToggle(string label,bool value,Action<bool> set)
    { if(ImGui.Checkbox(label,ref value)){set(value);Pi.SavePluginConfig(config);} }
    private void DrawChatMessageSettings()
    {
        ImGui.TextWrapped("Choose which Equinox messages appear in your own chat. These controls do not disable tracking or syncing.");
        ImGui.Separator();ImGui.TextUnformatted("Housing");
        MessageToggle("House entry messages",config.NotifyHouseEntries,v=>config.NotifyHouseEntries=v);
        MessageToggle("Housing reminders after 30 days",config.NotifyHousingWarnings,v=>config.NotifyHousingWarnings=v);
        ImGui.Separator();ImGui.TextUnformatted("Gardening");
        MessageToggle("Garden chat messages enabled",config.NotifyGardenCare,v=>config.NotifyGardenCare=v);
        ImGui.BeginDisabled(!config.NotifyGardenCare);
        MessageToggle("Tending due",config.NotifyGardenTending,v=>config.NotifyGardenTending=v);
        MessageToggle("Confirmed harvest-ready crops",config.NotifyGardenHarvest,v=>config.NotifyGardenHarvest=v);
        MessageToggle("Confirmed dead crops",config.NotifyGardenDead,v=>config.NotifyGardenDead=v);
        MessageToggle("Estimated maturity: check in game",config.NotifyGardenMaturity,v=>config.NotifyGardenMaturity=v);
        MessageToggle("Unknown garden care time: check tending",config.NotifyGardenUnknownCare,v=>config.NotifyGardenUnknownCare=v);
        ImGui.EndDisabled();
        ImGui.TextWrapped("One compact summary for this character's owned/shared houses. Green: harvest; blue: tending; orange: estimated risk; red: confirmed dead. Each login shows one compact reminder again. While logged in, unchanged warnings stay quiet; changing areas does not repeat them.");
        ImGui.Separator();ImGui.TextUnformatted("Commands and Fashion Report");
        MessageToggle("Fashion Report browser link",config.NotifyFashionLink,v=>config.NotifyFashionLink=v);
        MessageToggle("Planting command: house not identified",config.NotifyPlantingUnavailable,v=>config.NotifyPlantingUnavailable=v);
        MessageToggle("Fashion Report: could not open browser",config.NotifyBrowserErrors,v=>config.NotifyBrowserErrors=v);
    }
    private bool GardenMessageEnabled(string kind)=>kind switch
    {
        "tend" or "wilt"=>config.NotifyGardenTending,
        "harvest"=>config.NotifyGardenHarvest,
        "dead"=>config.NotifyGardenDead,
        "check maturity"=>config.NotifyGardenMaturity,
        _=>config.NotifyGardenUnknownCare
    };
}
