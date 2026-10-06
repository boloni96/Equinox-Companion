using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using NativeTreasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
    // Only keep values, never dereference cached native pointers after an addon is rebuilt.
    private readonly Dictionary<nint, (byte R, byte G, byte B, byte A, byte AppliedR, byte AppliedG, byte AppliedB, byte AppliedA)> cofferTints = [];
    private readonly HashSet<nint> cofferLiveNodes = [];

    private void OnCofferTerritoryChanged(uint territory) { cofferMemory.Clear(); cofferRendered.Clear(); cofferTerritory=territory; nextCofferCheck=default; }
    private unsafe void SetCofferMarkers(bool enabled)
    {
        config.EnableCofferMarkers = enabled;
        RestoreCofferTints();
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
        if (!config.EnableCofferMarkers || !config.CofferMinimap || cofferFault || Objects.LocalPlayer is not {} self) return;
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
    private unsafe void RestoreCofferTints()
    {
        UpdateCofferNativeTints(false);
    }
    private unsafe void DrawCofferMarkersCore()
    {
        UpdateCofferNativeTints(config.EnableCofferMarkers && config.CofferMinimap && !cofferFault);
        if (config.EnableCofferMarkers && config.CofferMainMap && !cofferFault) DrawCofferMainMap();
    }
    private unsafe void UpdateCofferNativeTints(bool enabled)
    {
        if(!enabled && cofferTints.Count==0)return;
        cofferLiveNodes.Clear();
        var addon = (AddonNaviMap*)GardenGui.GetAddonByName("_NaviMap").Address;
        if (addon == null) { cofferTints.Clear(); return; }
        foreach (var marker in addon->NaviMap.NaviMapMarkers)
        {
            if (marker.ComponentNode == null) continue;
            var node = &marker.ComponentNode->AtkResNode;
            var key = (nint)node;
            cofferLiveNodes.Add(key);
            var match = enabled && marker.IconId == CofferIcon ? cofferRendered.FindIndex(p => p.X == marker.X && p.Y == marker.Y) : -1;
            var opened = match >= 0 && cofferRendered[match].Opened;
            byte r = opened ? (byte)45 : (byte)255, g = opened ? (byte)255 : (byte)45, b=45;
            var custom=false;
            if(match>=0 && CofferStyles.Normalize(config.CofferStyle)!=0 && MarkerVisible(node))
            {
                Bounds bounds;node->GetBounds(&bounds);
                if(bounds.Width is >=4 and <=80 && bounds.Height is >=4 and <=80)
                {
                    var center=ImGui.GetMainViewport().Pos+new Vector2((bounds.Pos1.X+bounds.Pos2.X)/2,(bounds.Pos1.Y+bounds.Pos2.Y)/2);
                    DrawCofferSymbol(ImGui.GetForegroundDrawList(),center,Math.Clamp(bounds.Width*.4f,6,13),opened,config.CofferStyle);
                    custom=true;
                }
            }
            byte alpha=custom?(byte)0:(cofferTints.TryGetValue(key,out var previous)?previous.A:node->Color.A);
            if(match >= 0 && cofferTints.TryGetValue(key,out var kept) &&
                kept.AppliedR==r && kept.AppliedG==g && kept.AppliedB==b && kept.AppliedA==alpha &&
                node->MultiplyRed==r && node->MultiplyGreen==g && node->MultiplyBlue==b && node->Color.A==alpha)continue;
            // Restore before deciding whether the game has recycled this node for another icon.
            if (cofferTints.Remove(key, out var old) && node->MultiplyRed == old.AppliedR &&
                node->MultiplyGreen == old.AppliedG && node->MultiplyBlue == old.AppliedB && node->Color.A==old.AppliedA)
            {
                node->MultiplyRed=old.R; node->MultiplyGreen=old.G; node->MultiplyBlue=old.B;node->SetAlpha(old.A);
                node->IsDirty=true;
            }
            if (match < 0) continue;
            cofferTints[key]=(node->MultiplyRed,node->MultiplyGreen,node->MultiplyBlue,node->Color.A,r,g,b,alpha);
            node->MultiplyRed=r; node->MultiplyGreen=g; node->MultiplyBlue=b; node->SetAlpha(alpha); node->IsDirty=true;
        }
        foreach (var key in cofferTints.Keys.Where(k => !cofferLiveNodes.Contains(k)).ToArray()) cofferTints.Remove(key);
    }
    private unsafe void DrawCofferMainMap()
    {
        var map = AgentMap.Instance();
        var addon = (AddonAreaMap*)GardenGui.GetAddonByName("AreaMap").Address;
        if (map == null || addon == null || !addon->IsVisible || addon->ComponentMap == null ||
            !CofferMapProjection.SameMap(cofferTerritory, cofferMap, map->SelectedTerritoryId, map->SelectedMapId)) return;
        var component = addon->ComponentMap;
        if (component->BaseMapImage == null || component->OwnerNode == null ||
            !MarkerVisible(&component->BaseMapImage->AtkResNode)) return;
        Bounds textureBounds, clipBounds;
        component->BaseMapImage->GetBounds(&textureBounds);
        component->OwnerNode->GetBounds(&clipBounds);
        var origin = ImGui.GetMainViewport().Pos;
        var textureMin=origin+new Vector2(textureBounds.Pos1.X,textureBounds.Pos1.Y);
        var textureSize=new Vector2(textureBounds.Width,textureBounds.Height);
        var clipMin=origin+new Vector2(clipBounds.Pos1.X,clipBounds.Pos1.Y);
        var clipMax=origin+new Vector2(clipBounds.Pos2.X,clipBounds.Pos2.Y);
        if (textureSize.X<=0 || textureSize.Y<=0 || clipMin.X>=clipMax.X || clipMin.Y>=clipMax.Y) return;

        var draw=ImGui.GetForegroundDrawList();
        draw.PushClipRect(clipMin,clipMax,true);
        try
        {
            foreach(var point in cofferMemory.Points)
            {
                var uv=CofferMapProjection.TexturePosition(point.Position,map->SelectedOffsetX,map->SelectedOffsetY,map->SelectedMapSizeFactorFloat);
                if(!float.IsFinite(uv.X)||!float.IsFinite(uv.Y)||uv.X<0||uv.Y<0||uv.X>1||uv.Y>1)continue;
                var center=textureMin+uv*textureSize;
                if(center.X<clipMin.X||center.X>clipMax.X||center.Y<clipMin.Y||center.Y>clipMax.Y)continue;
                DrawCofferSymbol(draw,center,12*addon->Scale,point.Opened,config.CofferStyle);
            }
        }
        finally { draw.PopClipRect(); }
    }
    private unsafe void RefreshCofferMinimap()
    {
        var map=AgentMap.Instance();
        if(map!=null && Objects.LocalPlayer!=null)map->CreateMiniMapMarkers(false);
    }
    private void DrawCofferSettings()
    {
        MessageToggle("Show coffers on minimap", config.CofferMinimap, v => {
            config.CofferMinimap=v; RestoreCofferTints(); cofferRendered.Clear(); RefreshCofferMinimap();
        });
        MessageToggle("Show coffers on main map", config.CofferMainMap, v => config.CofferMainMap=v);
        DrawCofferAppearance();
        ImGui.TextWrapped(cofferStatus);
        ImGui.TextWrapped("Red: unopened. Green: confirmed opened, including by another player. Opened locations remain for this territory visit; logout or changing territory clears them. Ordinary treasure objects are supported; special Event Object coffers need separate support. No Journal sync and no automatic opening.");
        if(ImGui.Button("Clear this visit's coffer markers"))SetCofferMarkers(true);
        if(cofferFault&&ImGui.Button("Retry coffer markers"))SetCofferMarkers(true);
    }
}
