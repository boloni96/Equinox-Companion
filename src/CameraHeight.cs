using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly CameraHeightPolicy cameraHeightPolicy = new();
    private double cameraHeightNext;
    private bool cameraCommandRegistered, cameraHeightApplyPending, cameraHeightDefaultPending;
    private void InitializeCameraCommand()=>cameraCommandRegistered=Commands.AddHandler("/cam",new Dalamud.Game.Command.CommandInfo((_,_)=>SetCameraHeightEnabled(!config.RememberCameraHeight)){HelpMessage="Toggle saved camera height. Off restores game height; preferences remain saved."});
    private void DisposeCameraCommand(){if(cameraCommandRegistered)Commands.RemoveHandler("/cam");}
    private void SetCameraHeightEnabled(bool enabled){config.RememberCameraHeight=enabled;cameraHeightApplyPending=enabled;cameraHeightDefaultPending=!enabled;cameraHeightPolicy.Reset();cameraHeightNext=0;SaveConfiguration();}

    private uint cameraHeightTerritory;
    private ulong cameraHeightCharacter;
    private nint cameraHeightAddress;
    private string cameraHeightStatus = "Waiting for a normal third-person camera.";

    // EasyZoomReborn GameCamera.lookAtHeightOffset, checked against its current source.
    // This field is not yet named in FFXIVClientStructs. Keep the access isolated here;
    // do not alter its reset flag, interpolated values, collision, zoom, or FOV.
    private static unsafe float* HeightField(Camera* camera) => (float*)((byte*)camera + 0x234);

    private unsafe Camera* NormalHeightCamera()
    {
        if (!Player.IsLoaded || !Client.IsLoggedIn || Client.IsGPosing || Client.IsPvP ||
            HelperInCutscene() || Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51] ||
            Conditions[ConditionFlag.OccupiedInEvent] || Conditions[ConditionFlag.OccupiedInQuestEvent]) return null;
        var manager = CameraManager.Instance();
        if (manager == null || manager->ActiveCameraIndex != 0) return null;
        var camera = manager->Camera;
        if (camera == null || camera->ZoomMode != CameraZoomMode.ThirdPerson || camera->IsEventCameraAutoControl ||
            (camera->ControlMode != CameraControlMode.ThirdPersonLegacy && camera->ControlMode != CameraControlMode.ThirdPersonFixed)) return null;
        return camera;
    }

    private unsafe void UpdateCameraHeight()
    {
        if (!config.RememberCameraHeight && !cameraHeightDefaultPending) { cameraHeightPolicy.Reset(); return; }
        var seconds = (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        // Safety is checked every frame, including short loading/cutscene transitions.
        var camera = NormalHeightCamera();
        if (camera == null)
        { cameraHeightPolicy.Reset(); cameraHeightStatus = "Paused during loading or a special camera mode."; return; }
        if (cameraHeightTerritory != Client.TerritoryType || cameraHeightCharacter != Player.ContentId || cameraHeightAddress != (nint)camera)
        {
            cameraHeightTerritory = Client.TerritoryType; cameraHeightCharacter = Player.ContentId;
            cameraHeightAddress = (nint)camera; cameraHeightPolicy.Reset(); cameraHeightApplyPending=config.RememberCameraHeight;
        }
        if (seconds < cameraHeightNext) return;
        cameraHeightNext = seconds + .25;
        var current = *HeightField(camera);
        if (!CameraHeightPolicy.Valid(current))
        { cameraHeightPolicy.Reset(); cameraHeightStatus = "Camera value unavailable; no changes made."; return; }
        if(cameraHeightDefaultPending){*HeightField(camera)=0;cameraHeightDefaultPending=false;cameraHeightStatus="Game camera height restored · saved height retained.";return;}
        if (cameraHeightApplyPending && CameraHeightPolicy.Valid(config.PreferredCameraHeight) || cameraHeightPolicy.Restore(true, current, config.PreferredCameraHeight, seconds))
        {
            *HeightField(camera) = config.PreferredCameraHeight; cameraHeightApplyPending=false;
            cameraHeightStatus = "Restored saved height after a reset.";
        }
        else cameraHeightStatus = "Saved height enabled · watching for camera changes.";
    }

    private unsafe void DrawCameraHeightSettings()
    {
        ImGui.TextUnformatted("Camera height");
        MessageToggle("Remember camera height", config.RememberCameraHeight, value => {
            SetCameraHeightEnabled(value);
        });
        SettingsHelp("Restores your saved Look At Height Offset when the game changes it. /cam toggles saved/game height. Local to this PC; does not change zoom or FOV. Pauses during loading, cutscenes, dialogue, GPose and first-person view.");
        var preferred = config.PreferredCameraHeight;
        if (ImGui.SliderFloat("Saved height offset", ref preferred, -3f, 3f, "%.3f"))
        { config.PreferredCameraHeight = preferred; cameraHeightApplyPending=config.RememberCameraHeight; cameraHeightPolicy.Reset(); SaveConfiguration(); }
        var camera = NormalHeightCamera();
        var available = camera != null && CameraHeightPolicy.Valid(*HeightField(camera));
        ImGui.BeginDisabled(!available);
        if (ImGui.Button("Remember current height") && available)
        { config.PreferredCameraHeight = *HeightField(camera); cameraHeightPolicy.Reset(); SaveConfiguration(); }
        ImGui.SameLine();
        if (ImGui.Button(config.RememberCameraHeight?"Saved height ON · turn off":"Saved height OFF · turn on") && available && CameraHeightPolicy.Valid(config.PreferredCameraHeight))
        { SetCameraHeightEnabled(!config.RememberCameraHeight); }
        ImGui.EndDisabled();
        if (available) ImGui.TextUnformatted($"Current height: {*HeightField(camera):0.000}");
        if (config.RememberCameraHeight) ImGui.TextWrapped(cameraHeightStatus);
        SettingsHelp("Adjust EasyZoomReborn first, then Remember current height. Your saved value is kept when disabled. Off restores the game height; on reapplies your saved height. Set a /cam shortcut in Keybinds.");
    }
}
