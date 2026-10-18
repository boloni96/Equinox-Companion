using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly CameraHeightPolicy cameraHeightPolicy = new();
    private double cameraHeightNext;
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
        if (!config.RememberCameraHeight) { cameraHeightPolicy.Reset(); return; }
        var seconds = (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        // Safety is checked every frame, including short loading/cutscene transitions.
        var camera = NormalHeightCamera();
        if (camera == null)
        { cameraHeightPolicy.Reset(); cameraHeightStatus = "Paused during loading or a special camera mode."; return; }
        if (cameraHeightTerritory != Client.TerritoryType || cameraHeightCharacter != Player.ContentId || cameraHeightAddress != (nint)camera)
        {
            cameraHeightTerritory = Client.TerritoryType; cameraHeightCharacter = Player.ContentId;
            cameraHeightAddress = (nint)camera; cameraHeightPolicy.Reset();
        }
        if (seconds < cameraHeightNext) return;
        cameraHeightNext = seconds + .25;
        var current = *HeightField(camera);
        if (!CameraHeightPolicy.Valid(current))
        { cameraHeightPolicy.Reset(); cameraHeightStatus = "Camera value unavailable; no changes made."; return; }
        if (cameraHeightPolicy.Restore(true, current, config.PreferredCameraHeight, seconds))
        {
            *HeightField(camera) = config.PreferredCameraHeight;
            cameraHeightStatus = "Restored saved height after a reset.";
        }
        else cameraHeightStatus = "Watching for a reset to the default height.";
    }

    private unsafe void DrawCameraHeightSettings()
    {
        ImGui.TextUnformatted("Camera height");
        MessageToggle("Remember camera height", config.RememberCameraHeight, value => {
            config.RememberCameraHeight = value; cameraHeightPolicy.Reset();
        });
        ImGui.TextWrapped("Restores Look At Height Offset when the game resets it to zero. Local to this PC; does not change zoom or FOV. Pauses during loading, cutscenes, dialogue, GPose and first-person view.");
        var preferred = config.PreferredCameraHeight;
        if (ImGui.SliderFloat("Saved height offset", ref preferred, -3f, 3f, "%.3f"))
        { config.PreferredCameraHeight = preferred; cameraHeightPolicy.Reset(); SaveConfiguration(); }
        var camera = NormalHeightCamera();
        var available = camera != null && CameraHeightPolicy.Valid(*HeightField(camera));
        ImGui.BeginDisabled(!available);
        if (ImGui.Button("Remember current height") && available)
        { config.PreferredCameraHeight = *HeightField(camera); cameraHeightPolicy.Reset(); SaveConfiguration(); }
        ImGui.SameLine();
        if (ImGui.Button("Apply saved height") && available && CameraHeightPolicy.Valid(config.PreferredCameraHeight))
        { *HeightField(camera) = config.PreferredCameraHeight; cameraHeightPolicy.Reset(); }
        ImGui.EndDisabled();
        if (available) ImGui.TextUnformatted($"Current height: {*HeightField(camera):0.000}");
        if (config.RememberCameraHeight) ImGui.TextWrapped(cameraHeightStatus);
        ImGui.TextWrapped("Adjust EasyZoomReborn first, then Remember current height. Nonzero changes are left alone. Turn this off before intentionally resetting height to zero. Disabling stops restoration without moving the camera.");
    }
}
