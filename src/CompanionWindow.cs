using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly WindowSystem windows = new("EquinoxCompanion");
    private CompanionWindow mainWindow = null!;
    private sealed class CompanionWindow : Window
    {
        private readonly Plugin plugin;
        public CompanionWindow(Plugin plugin) : base("Equinox Companion")
        {
            this.plugin = plugin;
            IsOpen = true;
            Size = new Vector2(660, 480);
            SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(500, 360), MaximumSize = new Vector2(float.MaxValue) };
            AllowPinning = true;
            AllowClickthrough = true;
            AllowBackgroundBlur = true;
        }
        public override void Draw() => plugin.DrawContents();
    }
}
