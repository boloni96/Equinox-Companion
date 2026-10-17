using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private static void CopyTextOnClick(string text)
 {
  if(ImGui.IsItemClicked())ImGui.SetClipboardText(text);
  if(ImGui.IsItemHovered())ImGui.SetTooltip("Click to copy");
 }
}
