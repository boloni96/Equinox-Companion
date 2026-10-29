using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly WindowResizeGate organizerResizeGate=new();
    private void DrawOrganizerWindow()
    {
        var size=ImGui.GetWindowSize();var cursor=ImGui.GetMouseCursor();
        var resize=cursor is ImGuiMouseCursor.ResizeNWSE or ImGuiMouseCursor.ResizeNESW or ImGuiMouseCursor.ResizeEW or ImGuiMouseCursor.ResizeNS;
        var position=ImGui.GetWindowPos();var mouse=ImGui.GetMousePos();
        resize&=WindowResizeGate.OnBorder(mouse.X-position.X,mouse.Y-position.Y,size.X,size.Y);
        var paused=organizerResizeGate.Update(size.X,size.Y,ImGui.IsMouseDown(ImGuiMouseButton.Left),resize,Environment.TickCount64);
        DrawWindowWithLauncher(paused?DrawOrganizerResizeNotice:DrawRetriever);
    }
    private void DrawOrganizerResizeNotice()
    {
        ImGui.TextDisabled("Resizing…");
        windowStatus=organizeRunning?organizeStatus:retrievalRun.Running?retrievalRun.Status:chestRunning?chestStatus:"Release to show Organizer";
    }
}
