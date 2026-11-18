using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private FashionReportWindow fashionWindow = null!;
    private sealed class FashionReportWindow : Window, IDisposable
    {
        private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(25), MaxResponseContentBufferSize = 5_000_000 };
        private readonly CancellationTokenSource cancel = new();
        private Task<(IDalamudTextureWrap Texture, string Title)>? loading;
        private IDalamudTextureWrap? picture;
        private string title = "Fashion Report XIV · V1";
        private string status = "";
        private DateTimeOffset nextCheck;
        private int loadingWeek;
        private int pictureWeek;
        private bool expanded;
        private bool resize;
        private float zoom = 1;
        private Vector2 pan;
        private bool dragged;
        private bool picturePressed;
        public Action? Minimize { get; set; }
        private readonly Plugin plugin;
        private readonly Action openBrowser;
        public FashionReportWindow(Plugin plugin,Action openBrowser) : base("Fashion Report###EquinoxFashion", ImGuiWindowFlags.None)
        {
            this.plugin=plugin;
            this.openBrowser = openBrowser;
            Size = new Vector2(720, 490);
            SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 260), MaximumSize = new Vector2(float.MaxValue) };
            AllowPinning = true; RespectCloseHotkey = false;
            AllowBackgroundBlur = true;
        }
        public void OpenReport()
        {
            IsOpen = true;
            Tick();
        }
        private void Refresh()
        {
            var now = DateTimeOffset.UtcNow;
            if (loading is not null || !FashionCompletion.IsOpen(now)) return;
            loadingWeek = FashionCompletion.WeekNumber(now);
            var reset = FashionCompletion.Window(now).End;
            nextCheck = now.AddHours(1) < reset ? now.AddHours(1) : reset;
            status = "Loading current V1 picture…";
            loading = LoadAsync(loadingWeek);
        }
        private async Task<string> ReadTitleAsync(int week)
        {
            var url = "https://fashionreportxiv.com/api/report-state?equinox=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, cancel.Token));
            var options = doc.RootElement.GetProperty("lastOptions");
            if (!int.TryParse(options.GetProperty("week").ToString(), out var publishedWeek) || publishedWeek != week)
                throw new InvalidDataException($"Waiting for the week {week} guide.");
            var theme = options.GetProperty("reportTitle").GetString();
            if (string.IsNullOrWhiteSpace(theme)) throw new InvalidDataException("Missing Fashion Report theme.");
            return $"Week {week} · {theme} · V1";
        }
        private async Task<(IDalamudTextureWrap, string)> LoadAsync(int week)
        {
            var label = await ReadTitleAsync(week);
            var bytes = await http.GetByteArrayAsync("https://fashionreportxiv.com/hint.png?equinox=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), cancel.Token);
            if (await ReadTitleAsync(week) != label) throw new InvalidDataException("The guide changed during download.");
            var texture = await Textures.CreateFromImageAsync(bytes, "Equinox Fashion Report V1", cancel.Token);
            return (texture, label);
        }
        public void Tick()
        {
            var now = DateTimeOffset.UtcNow;
            if (loading is { IsCompleted: false } || (loading is null && now < nextCheck)) return;
            var week = FashionCompletion.WeekNumber(now);
            if (picture is not null && pictureWeek != week)
            {
                picture.Dispose();
                picture = null;
                title = "Fashion Report XIV · V1";
            }
            if (loading is { } completed)
            {
                loading = null;
                if (completed.IsCompletedSuccessfully)
                {
                    var result = completed.Result;
                    if (week == loadingWeek)
                    {
                        picture?.Dispose();
                        (picture, title) = result;
                        pictureWeek = week;
                        status = "Scroll to zoom · Drag to move · Click to expand/shrink. · Kaiyoko Star / Fashion Report XIV";
                    }
                    else result.Texture.Dispose();
                }
                else
                {
                    Log.Warning(completed.Exception, "Fashion Report picture download failed.");
                    status = picture is null ? $"Week {week} guide unavailable. Retrying in an hour, or choose Refresh." : "Refresh failed. Showing the verified picture for this week.";
                }
            }
            if (!FashionCompletion.IsOpen(now))
            {
                nextCheck = FashionCompletion.Window(now).Start;
                status = "Judging is closed. The next guide check is Friday at 08:00 UTC.";
            }
            else if (now >= nextCheck) Refresh();
        }
        public override void PostDraw()=>plugin.MeasureWindow(WindowName,PostDrawMeasured,"PostDraw");
        private void PostDrawMeasured() => HandleNativeCollapse(this, Minimize);
        public override void Draw()=>plugin.MeasureWindow(WindowName,DrawMeasuredContent);
        private void DrawMeasuredContent()
        {
            if (resize)
            {
                var display = ImGui.GetIO().DisplaySize;
                ImGui.SetWindowSize(new Vector2(Math.Min(expanded ? 1500 : 720, display.X - 30), Math.Min(expanded ? 960 : 490, display.Y - 50)));
                resize = false;
            }
            plugin.DrawWindowWithLauncher(DrawContent);
        }
        private void DrawContent()
        {
            ImGui.TextWrapped(title);
            ImGui.BeginDisabled(loading is not null || !FashionCompletion.IsOpen(DateTimeOffset.UtcNow));
            if (ImGui.Button("Refresh")) Refresh();
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Open in browser")) openBrowser();
            ImGui.SameLine();
            if (ImGui.Button("Reset view")) { zoom = 1; pan = Vector2.Zero; }
            ImGui.TextWrapped(status);
            if (picture is null) return;
            var available = ImGui.GetContentRegionAvail();
            if (available.X < 1 || available.Y < 1) return;
            if (ImGui.BeginChild("ReportViewport", available, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                var origin = ImGui.GetCursorScreenPos();
                var space = Vector2.Max(ImGui.GetContentRegionAvail(), Vector2.One);
                var fit = Math.Min(space.X / picture.Width, space.Y / picture.Height);
                var clicked = ImGui.InvisibleButton("ReportCanvas", space, ImGuiButtonFlags.MouseButtonLeft);
                var currentSize = new Vector2(picture.Width, picture.Height) * fit * zoom;
                var currentPosition = origin + (space - currentSize) / 2 + pan;
                var hovered = ImGui.IsItemHovered() && ImGui.IsMouseHoveringRect(Vector2.Max(origin, currentPosition), Vector2.Min(origin + space, currentPosition + currentSize));
                if (ImGui.IsItemActivated()) { dragged = false; picturePressed = hovered; }
                if (hovered && ImGui.GetIO().MouseWheel != 0)
                {
                    var before = zoom;
                    zoom = Math.Clamp(zoom * MathF.Pow(1.2f, ImGui.GetIO().MouseWheel), 1, 8);
                    var relative = ImGui.GetMousePos() - origin - space / 2;
                    pan = relative - (relative - pan) * (zoom / before);
                }
                if (picturePressed && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    dragged = true;
                    pan += ImGui.GetIO().MouseDelta;
                }
                if (clicked && picturePressed && !dragged) { expanded = !expanded; resize = true; }
                var size = new Vector2(picture.Width, picture.Height) * fit * zoom;
                var bounds = Vector2.Max((size - space) / 2, Vector2.Zero);
                pan = Vector2.Clamp(pan, -bounds, bounds);
                var position = origin + (space - size) / 2 + pan;
                var draw = ImGui.GetWindowDrawList();
                draw.PushClipRect(origin, origin + space, true);
                draw.AddImage(picture.Handle, position, position + size);
                draw.PopClipRect();
                if (hovered && !ImGui.IsItemActive()) ImGui.SetTooltip($"Zoom {zoom:0.0}× · Scroll to zoom · Drag to move · Click to {(expanded ? "shrink" : "expand")}");
            }
            ImGui.EndChild();
        }
        public void Dispose()
        {
            cancel.Cancel();
            picture?.Dispose();
            picture = null;
            if (loading is {} pending)
                _ = pending.ContinueWith(t => { if (t.IsCompletedSuccessfully) t.Result.Texture.Dispose(); else _ = t.Exception; cancel.Dispose(); }, TaskScheduler.Default);
            else cancel.Dispose();
            loading = null;
            http.Dispose();
        }
    }
}
