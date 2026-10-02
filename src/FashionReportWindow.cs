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
        public FashionReportWindow() : base("Fashion Report###EquinoxFashion")
        {
            Size = new Vector2(720, 490);
            SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 260), MaximumSize = new Vector2(float.MaxValue) };
            AllowPinning = true;
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
                    status = "Click the picture to expand or shrink. · Kaiyoko Star / Fashion Report XIV";
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
            if (ImGui.Button("Open in browser")) OpenFashionBrowser();
            ImGui.TextWrapped(status);
            if (picture is null) return;
            var space = ImGui.GetContentRegionAvail();
            var scale = Math.Max(0.01f, Math.Min(space.X / picture.Width, space.Y / picture.Height));
            ImGui.Image(picture.Handle, new Vector2(picture.Width, picture.Height) * scale);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(expanded ? "Click to shrink" : "Click to expand");
            if (ImGui.IsItemClicked()) { expanded = !expanded; resize = true; }
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
