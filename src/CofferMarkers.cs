using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using NativeTreasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly CofferMemory cofferMemory = new();
    private DateTimeOffset nextCofferCheck;
    private uint cofferTerritory, cofferMap;
    private ulong cofferCharacter;
    private bool cofferFault;
    private string cofferStatus = "Disabled.";
    private unsafe delegate void CreateCofferMarkersDelegate(AgentMap* map, bool omitAetherytes);
    private Hook<CreateCofferMarkersDelegate>? cofferHook;
    // Game chest symbol (MapSymbol 9); native layout supplies rotation/zoom/clipping.
    private const uint CofferIcon = 60460;
    private readonly List<(short X, short Y, bool Opened)> cofferRendered = [];

    private void OnCofferTerritoryChanged(uint territory) { cofferMemory.Clear(); cofferRendered.Clear(); cofferTerritory=territory; nextCofferCheck=default; }
    private unsafe void SetCofferMarkers(bool enabled)
    {
        config.EnableCofferMarkers = enabled;
        cofferFault = false; cofferMemory.Clear(); cofferRendered.Clear(); nextCofferCheck = default;
        if (enabled)
        {
            try
            {
                cofferHook ??= Interop.HookFromAddress<CreateCofferMarkersDelegate>(AgentMap.MemberFunctionPointers.CreateMiniMapMarkers, AppendCofferMarkers);
                cofferHook.Enable(); cofferStatus = "Watching nearby treasure coffers.";
                var map = AgentMap.Instance(); if(map != null && Objects.LocalPlayer != null) map->CreateMiniMapMarkers(false);
            }
            catch (Exception e)
            {
                cofferFault = true; cofferStatus = "Minimap integration unavailable; markers paused.";
                errorJournal.Record("coffer-markers", cofferStatus, exceptionType:e.GetType().Name);
            }
        }
        else
        {
            var wasActive = cofferHook?.IsEnabled == true;
            cofferHook?.Disable(); cofferStatus = "Disabled.";
            if (wasActive)
            {
                try { var map = AgentMap.Instance(); if (map != null && Objects.LocalPlayer != null) map->CreateMiniMapMarkers(false); }
                catch (Exception e) { errorJournal.Record("coffer-cleanup", "Minimap cleanup deferred to next native refresh",exceptionType:e.GetType().Name); }
            }
        }
    }
    private unsafe void AppendCofferMarkers(AgentMap* map, bool omitAetherytes)
    {
        cofferHook!.Original(map, omitAetherytes);
        cofferRendered.Clear();
        if (!config.EnableCofferMarkers || cofferFault || Objects.LocalPlayer is not {} self) return;
        try
        {
            foreach (var point in cofferMemory.Points.OrderBy(p => Vector3.DistanceSquared(p.Position,self.Position)).Take(32))
            {
                // Never duplicate the game's coffer at this location; never overwrite native markers.
                var x = (short)(point.Position.X * 16); var y = (short)(point.Position.Z * 16);
                var found = false;
                for (var i = 0; i < map->MiniMapMarkerCount; i++)
                {
                    var existing = map->MiniMapMarkers[i].MapMarker;
                    if (existing.IconId == CofferIcon && existing.X == x && existing.Y == y) { found = true; break; }
                }
                if (!found && !map->AddMiniMapMarker(point.Position,CofferIcon)) continue;
                cofferRendered.Add((x,y,point.Opened));
            }
        }
        catch (Exception e)
        {
            cofferFault = true; cofferStatus = "Minimap integration paused after an error.";
            errorJournal.Record("coffer-markers",cofferStatus,exceptionType:e.GetType().Name);
        }
    }
    private unsafe void UpdateCofferMarkers(DateTimeOffset now)
    {
        if (!config.EnableCofferMarkers) return;
        if (cofferHook == null && !cofferFault) SetCofferMarkers(true);
        if (cofferFault || now < nextCofferCheck) return;
        nextCofferCheck = now.AddMilliseconds(500);
        try
        {
            var cid = Player.IsLoaded ? Player.ContentId : 0;
            var currentMap = AgentMap.Instance();
            var mapId = currentMap != null ? currentMap->CurrentMapId : 0;
            if (cid != cofferCharacter || cofferTerritory != Client.TerritoryType || cofferMap != mapId)
            { cofferMemory.Clear(); cofferRendered.Clear(); cofferCharacter=cid; cofferTerritory=Client.TerritoryType; cofferMap=mapId; }
            if (cid == 0 || Objects.LocalPlayer == null) return;
            var seen = new List<CofferPoint>();
            var loot = Loot.Instance();
            foreach (var item in Objects)
            {
                if (item.ObjectKind != ObjectKind.Treasure || item.Address == 0) continue;
                var treasure = (NativeTreasure*)item.Address;
                var opened = (treasure->Flags & NativeTreasure.TreasureFlags.Opened) != 0;
                if (!opened && loot != null)
                    foreach (var drop in loot->Items) if (drop.ChestObjectId == item.GameObjectId) { opened=true; break; }
                seen.Add(new(item.GameObjectId,item.BaseId,item.Position,opened,true));
            }
            if (cofferMemory.Observe(seen)) { var map = AgentMap.Instance(); if(map != null) map->CreateMiniMapMarkers(false); }
            cofferStatus = $"This visit: {cofferMemory.Points.Count(p=>!p.Opened)} unopened · {cofferMemory.Points.Count(p=>p.Opened)} confirmed opened.";
        }
        catch(Exception e)
        {
            cofferFault=true; cofferStatus="Coffer observation paused after an error.";
            errorJournal.Record("coffer-observation",cofferStatus,exceptionType:e.GetType().Name);
        }
    }
    private void DrawCofferMarkers()
    {
        try { DrawCofferMarkersCore(); }
        catch(Exception e) { cofferFault=true; cofferStatus="Coffer drawing paused after an error."; errorJournal.Record("coffer-drawing",cofferStatus,exceptionType:e.GetType().Name); }
    }
    private unsafe void DrawCofferMarkersCore()
    {
        if (!config.EnableCofferMarkers || cofferFault || cofferRendered.Count == 0) return;
        var addon=(AddonNaviMap*)GardenGui.GetAddonByName("_NaviMap").Address;
        if(addon==null||!addon->IsVisible)return;
        // Draw only at the game's own marker nodes: no estimated screen projection or world-map pin.
        foreach(var marker in addon->NaviMap.NaviMapMarkers)
        {
            if(marker.IconId!=CofferIcon || marker.ComponentNode==null || !MarkerVisible(&marker.ComponentNode->AtkResNode))continue;
            var found=false;var opened=false;
            foreach(var point in cofferRendered)
                if(point.X==marker.X&&point.Y==marker.Y){found=true;opened=point.Opened;break;}
            if(!found)continue;
            Bounds bounds; marker.ComponentNode->GetBounds(&bounds);
            if(bounds.Width<4||bounds.Width>80||bounds.Height<4||bounds.Height>80)continue;
            var center=ImGui.GetMainViewport().Pos+new Vector2((bounds.Pos1.X+bounds.Pos2.X)/2,(bounds.Pos1.Y+bounds.Pos2.Y)/2);
            var size=Math.Clamp(bounds.Width*.38f,5,12);
            var color=opened?0xff66dd55u:0xff5555ffu;
            var draw=ImGui.GetForegroundDrawList();
            draw.AddRectFilled(center-new Vector2(size,size*.7f),center+new Vector2(size,size*.7f),0xf0202020,2);
            draw.AddRect(center-new Vector2(size,size*.7f),center+new Vector2(size,size*.7f),color,2,ImDrawFlags.None,2);
            draw.AddLine(center-new Vector2(size,0),center+new Vector2(size,0),color,2);
            draw.AddRectFilled(center-new Vector2(1,2),center+new Vector2(1,2),color);
        }
    }
    private void DrawCofferSettings()
    {
        ImGui.TextWrapped(cofferStatus);
        ImGui.TextWrapped("Red: unopened. Green: confirmed opened, including by another player. Opened locations remain for this territory visit; logout or changing territory clears them. Ordinary treasure objects are supported; special Event Object coffers need separate support. No Journal sync and no automatic opening.");
        if(ImGui.Button("Clear this visit's coffer markers"))SetCofferMarkers(true);
        if(cofferFault&&ImGui.Button("Retry coffer markers"))SetCofferMarkers(true);
    }
}
