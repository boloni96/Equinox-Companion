using System.Diagnostics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly RollingPerformance rollingPerformance=new();
    private PerformanceReading[] performancePanelRows=[];
    private double nextPerformancePanelRead;
    private static double PerformanceClock=>(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    private void RecordPerformanceElapsed(string stage,long started)=>RecordPerformanceSample(stage,Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    private void DrawRecentPerformanceDiagnostics()
    {
        var clock=PerformanceClock;
        if(clock>=nextPerformancePanelRead){performancePanelRows=rollingPerformance.Snapshot(clock);nextPerformancePanelRead=clock+.25;}
        ImGui.TextDisabled("Last 60 seconds · milliseconds");
        SettingsHelp("Average and peak use only samples from the last 60 seconds. Hover for the last measurement and session peak. Stages can overlap; do not add them together. These timings measure Companion work, not game FPS or other plugins.");
        if(ImGui.BeginTable("Performance215",3,ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Work");ImGui.TableSetupColumn("Average");ImGui.TableSetupColumn("Peak");ImGui.TableHeadersRow();
            foreach(var row in performancePanelRows)
            {
                ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextUnformatted(row.Stage);
                if(ImGui.IsItemHovered())ImGui.SetTooltip($"Last: {row.LastMs:F2} ms · {row.AgeSeconds:F0}s ago\nSession peak: {row.LifetimePeakMs:F2} ms\nRecent samples: {row.RecentSamples}");
                ImGui.TableNextColumn();ImGui.TextUnformatted(row.RecentSamples>0?row.AverageMs.ToString("F2"):"—");
                ImGui.TableNextColumn();ImGui.TextUnformatted(row.RecentSamples>0?row.RecentPeakMs.ToString("F2"):"—");
            }
            ImGui.EndTable();
        }
        ImGui.TextWrapped("Exports include these timings and up to 256 slow-stage samples from the last 10 minutes, with activity context. No recording needs to be started.");
        if(ImGui.SmallButton("Export performance report")&&!DiagnosticExportBusy)Export();
        ImGui.TextDisabled(configurationDirty||configurationWriter.Busy?"Local save pending…":"Local save complete.");
        if(configurationError.Length>0)ImGui.TextWrapped(configurationError);
        if(ImGui.SmallButton("Reset measurements"))ResetPerformanceReport();
    }
}
