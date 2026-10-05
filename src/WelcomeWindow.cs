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
            ImGui.BeginGroup();ImGui.TextDisabled("EQUINOX COMPANION");
            ImGui.TextColored(new Vector4(.72f,.84f,1f,1f),"Welcome, Goddess");
            ImGui.SameLine();
            // A drawn blue heart stays visible even when the user's font has no emoji glyphs.
            var heart=ImGui.GetCursorScreenPos();var ink=ImGui.GetColorU32(new Vector4(.35f,.65f,1f,1f));var draw=ImGui.GetWindowDrawList();
            draw.AddCircleFilled(heart+new Vector2(5,5),4,ink);
            draw.AddCircleFilled(heart+new Vector2(11,5),4,ink);
            draw.AddTriangleFilled(heart+new Vector2(1,6),heart+new Vector2(15,6),heart+new Vector2(8,15),ink);
            ImGui.Dummy(new Vector2(17,17));
            ImGui.TextWrapped("Your Empire, with a little less to carry.");ImGui.EndGroup();
            ImGui.Spacing();ImGui.Separator();ImGui.Spacing();
            ImGui.TextWrapped("Equinox Companion was created to help you care for your Equinox Empire—from every little garden to every place you call home. A little less to remember, a little more time to enjoy the worlds you’ve made your own.");
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(.72f,.84f,1f,1f),"Made with care, for you.");
            ImGui.Spacing();ImGui.Separator();ImGui.Spacing();
            if(ImGui.CollapsingHeader("Companion guide & what's new"))
            {
                ImGui.BulletText("Characters, accounts and collections");
                ImGui.BulletText("Housing visits and garden care reminders");
                ImGui.BulletText("Gardening plans, submarine timers and supplies");
                ImGui.BulletText("Fashion Report, right here in game");
                ImGui.Spacing();ImGui.TextUnformatted("Version "+CompanionVersion);
                ImGui.TextWrapped("New: optional QuickLoot. Enable it in Settings > Tracking to show its tab. Shared loot rules, automatic Need / Greed / Pass, collection and equipment filters, rule previews and top-bar controls. Disabled by default. Gardening keeps the rollback behavior.");
                ImGui.Spacing();
                ImGui.TextWrapped(plugin.config.PairingKey.Length==64?"Your saved pairing and settings are kept. Journal V7.11.27 supports the new submarine cache sync; save once after updating the website.":"Start in Settings > Connection: paste the pairing key from your Journal's Game connection. Then choose what to sync in Settings > Tracking.");
                ImGui.TextWrapped("/equinox opens Companion. /gardening opens the garden guide at an identified paired house. /fashionr opens the Fashion Report.");
            }
            ImGui.Spacing();
            if(ImGui.Button("Open Companion")){plugin.visible=true;plugin.mainWindow.IsOpen=true;plugin.DismissWelcome();}
            ImGui.SameLine();if(ImGui.Button("Close"))plugin.DismissWelcome();
            ImGui.TextDisabled("Shown once per version. Reopen from Settings > Diagnostics.");
        }
    }
}
