using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static readonly string[] ShortcutKeys = ["None", .. Enumerable.Range('A',26).Select(x=>((char)x).ToString()), .. Enumerable.Range(1,12).Select(x=>"F"+x), .. Enumerable.Range(0,10).Select(x=>"Key"+x)];
    private (string Name, Shortcut Binding)[] Shortcuts() => [("/equinox",config.EquinoxShortcut),("/planting",config.PlantingShortcut),("/fashionr",config.FashionShortcut)];
    private unsafe void UpdateShortcuts()
    {
        var io=ImGui.GetIO();
        var game=FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        var ui=RaptureAtkModule.Instance();
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
        ImGui.TextWrapped("Choose a key and optional modifiers for each command. Changes save immediately and survive updates. Unassigned disables the shortcut.");
        foreach(var (name,binding) in Shortcuts())
        {
            ImGui.PushID(name);ImGui.Separator();ImGui.TextUnformatted(name+" — "+binding.Label);
            var changed=false;
            if(ImGui.BeginCombo("Key",binding.Key=="None"?"Unassigned":binding.Key.Replace("Key", "")))
            {
                foreach(var key in ShortcutKeys)if(ImGui.Selectable(key=="None"?"Unassigned":key.Replace("Key", ""),binding.Key==key)){binding.Key=key;changed=true;}
                ImGui.EndCombo();
            }
            var ctrl=binding.Ctrl;var alt=binding.Alt;var shift=binding.Shift;
            if(ImGui.Checkbox("Ctrl",ref ctrl)){binding.Ctrl=ctrl;changed=true;}ImGui.SameLine();
            if(ImGui.Checkbox("Alt",ref alt)){binding.Alt=alt;changed=true;}ImGui.SameLine();
            if(ImGui.Checkbox("Shift",ref shift)){binding.Shift=shift;changed=true;}
            if(ImGui.Button("Clear binding")){binding.Key="None";changed=true;}
            if(changed)Pi.SavePluginConfig(config);
            if(binding.Key!="None"&&Shortcuts().Count(x=>x.Binding.Matches(binding.Key,binding.Ctrl,binding.Alt,binding.Shift))>1)ImGui.TextWrapped("Duplicate shortcut: assign a different combination. Conflicting shortcuts are disabled.");
            ImGui.PopID();
        }
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
        MessageToggle("Estimated maturity: check in game",config.NotifyGardenMaturity,v=>config.NotifyGardenMaturity=v);
        MessageToggle("Unknown garden care time: check tending",config.NotifyGardenUnknownCare,v=>config.NotifyGardenUnknownCare=v);
        ImGui.EndDisabled();ImGui.Separator();ImGui.TextUnformatted("Commands and Fashion Report");
        MessageToggle("Fashion Report browser link",config.NotifyFashionLink,v=>config.NotifyFashionLink=v);
        MessageToggle("Planting command: house not identified",config.NotifyPlantingUnavailable,v=>config.NotifyPlantingUnavailable=v);
        MessageToggle("Fashion Report: could not open browser",config.NotifyBrowserErrors,v=>config.NotifyBrowserErrors=v);
    }
    private bool GardenMessageEnabled(string kind)=>kind switch
    {
        "tend"=>config.NotifyGardenTending,
        "harvest"=>config.NotifyGardenHarvest,
        "check maturity"=>config.NotifyGardenMaturity,
        _=>config.NotifyGardenUnknownCare
    };
}
