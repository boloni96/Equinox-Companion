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
        private DateTimeOffset lastLoaded;
        private DateTimeOffset lastAttempt;
        private bool expanded;
        private bool resize;
        private float zoom = 1;
        private Vector2 pan;
        private bool dragged;
        private bool picturePressed;
        public Action? Minimize { get; set; }
        private readonly Action openBrowser;
        public FashionReportWindow(Action openBrowser) : base("Fashion Report###EquinoxFashion", ImGuiWindowFlags.None)
        {
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
            if (picture is null || DateTimeOffset.UtcNow - lastLoaded > TimeSpan.FromHours(1)) Refresh();
        }
        private void Refresh()
        {
            if (loading is not null) return;
            lastAttempt = DateTimeOffset.UtcNow;
            status = "Loading current V1 picture…";
            loading = LoadAsync();
        }
        private async Task<(IDalamudTextureWrap, string)> LoadAsync()
        {
            var label = "Fashion Report XIV · V1";
            try
            {
                var text = await http.GetStringAsync("https://fashionreportxiv.com/api/report-state", cancel.Token);
                using var doc = JsonDocument.Parse(text);
                var options = doc.RootElement.GetProperty("lastOptions");
                label = "Week " + options.GetProperty("week").ToString() + " · " + options.GetProperty("reportTitle").GetString() + " · V1";
            }
            catch (Exception) when (!cancel.IsCancellationRequested) { /* The image can still be read when metadata is unavailable. */ }
            var bytes = await http.GetByteArrayAsync("https://fashionreportxiv.com/hint.png?equinox=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(), cancel.Token);
            var texture = await Textures.CreateFromImageAsync(bytes, "Equinox Fashion Report V1", cancel.Token);
            return (texture, label);
        }
        public void Tick(bool login = false)
        {
            if (loading?.IsCompleted == true)
            {
                if (loading.IsCompletedSuccessfully)
                {
                    picture?.Dispose();
                    (picture, title) = loading.Result;
                    lastLoaded = DateTimeOffset.UtcNow;
                    status = "Scroll to zoom · Drag to move · Click to expand/shrink. · Kaiyoko Star / Fashion Report XIV";
                }
                else
                {
                    Log.Warning(loading.Exception, "Fashion Report picture download failed.");
                    status = picture is null ? "Picture unavailable. Try Refresh or Open in browser." : "Refresh failed; showing the previously downloaded picture.";
                }
                loading = null;
            }
            if (loading is null && (login || DateTimeOffset.UtcNow - lastAttempt > TimeSpan.FromHours(1))) Refresh();
        }
        public override void PostDraw() => HandleNativeCollapse(this, Minimize);
        public override void Draw()
        {
            if (resize)
            {
                var display = ImGui.GetIO().DisplaySize;
                ImGui.SetWindowSize(new Vector2(Math.Min(expanded ? 1500 : 720, display.X - 30), Math.Min(expanded ? 960 : 490, display.Y - 50)));
                resize = false;
            }
            ImGui.TextWrapped(title);
            ImGui.BeginDisabled(loading is not null);
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
