using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static string CompanionVersion => typeof(Plugin).Assembly.GetName().Version?.ToString()??"0";
    private WelcomeWindow welcomeWindow = null!;
    private void DismissWelcome()
    {
        welcomeWindow.IsOpen=false;
        if(config.WelcomeVersion==CompanionVersion)return;
        config.WelcomeVersion=CompanionVersion;Pi.SavePluginConfig(config);
    }
    private sealed class WelcomeWindow : Window
    {
        private readonly Plugin plugin;
        public WelcomeWindow(Plugin plugin):base("Welcome to Equinox Companion###EquinoxWelcome",ImGuiWindowFlags.NoCollapse)
        {
            this.plugin=plugin;
            IsOpen=plugin.config.WelcomeVersion!=CompanionVersion;
            Size=new Vector2(580,550);SizeCondition=ImGuiCond.FirstUseEver;
            SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(460,440),MaximumSize=new Vector2(900,850)};
        }
        public override void OnClose()=>plugin.DismissWelcome();
        public override void Draw()
        {
            var icon=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"icon.png")).GetWrapOrDefault();
            if(icon is not null){ImGui.Image(icon.Handle,new Vector2(64,64));ImGui.SameLine();}
            ImGui.BeginGroup();ImGui.TextUnformatted("EQUINOX COMPANION");
            ImGui.TextColored(new Vector4(.82f,.72f,1f,1f),"Built for GODDESS.");
            ImGui.TextDisabled("Your characters. Your homes. Your shared world.");ImGui.EndGroup();
            ImGui.Spacing();ImGui.Separator();ImGui.Spacing();
            ImGui.TextWrapped("A little companion for a big world: bring your in-game progress to Equinox Journal, and keep the people, characters and homes you care about together.");
            ImGui.Spacing();
            ImGui.BulletText("Characters, accounts and collections");
            ImGui.BulletText("Housing visits and garden care reminders");
            ImGui.BulletText("Planting plans, submarine timers and supplies");
            ImGui.BulletText("Fashion Report, right here in game");
            ImGui.Spacing();ImGui.Separator();
            ImGui.TextUnformatted("What's new in "+CompanionVersion);
            ImGui.TextWrapped("AutoRetainer submarine caches and carried supplies. Click-to-set command shortcuts. Corrected submarine route syncing. This welcome, made for GODDESS.");
            ImGui.Spacing();
            ImGui.TextWrapped(plugin.config.PairingKey.Length==64?"Your saved pairing and settings are kept. Update Journal to V7.11.27 and save once to enable the new submarine cache sync.":"Start in Settings > Connection: paste the pairing key from your Journal's Game connection. Then choose what to sync in Settings > Tracking.");
            ImGui.TextWrapped("/equinox opens Companion. /planting opens the garden guide at an identified paired house. /fashionr opens the Fashion Report.");
            ImGui.Spacing();
            if(ImGui.Button("Open Companion")){plugin.visible=true;plugin.mainWindow.IsOpen=true;plugin.DismissWelcome();}
            ImGui.SameLine();if(ImGui.Button("Close"))plugin.DismissWelcome();
            ImGui.TextDisabled("Shown once per version. Reopen from Settings > Diagnostics.");
        }
    }
}
