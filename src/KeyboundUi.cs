// Adapted from KEWI-hub/KeyboundBrawler. MIT License
// 
// Copyright (c) 2026 KEWI
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// 
using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion.Keybound;

// Key is the text node's address, which stays the same for one word label while it lives.
// LabelVisible: everything above the text node is shown. TextVisible: the text node itself is shown too.
// Obscured: the label's noise overlay (#10) is up, so the label is hidden but its word is live and typeable.
public readonly record struct UiText(
    string Addon, uint NodeId, string Text, float X, float Y, byte FontSize, uint Color, int Depth,
    nint Key, bool LabelVisible, bool TextVisible, LabelTint Tint = LabelTint.None, bool Obscured = false);

// Colour of a KTGEWord label, read from the AddRGB tint on its background (node #26).
public enum LabelTint { None, Blue, Gray, Red, Gold, Green }

// Reads text straight out of the game's UI tree.
public static unsafe class KeyboundUi
{
    public static List<string> VisibleAddons()
    {
        var result = new List<string>();
        var stage = AtkStage.Instance();
        if (stage == null) return result;

        ref var list = ref stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
        for (var i = 0; i < Math.Min((int)list.Count,1024); i++)
        {
            var unit = list.Entries[i].Value;
            if (unit == null || !unit->IsVisible) continue;
            result.Add(unit->NameString);
        }
        return result;
    }

    // Visible text only.
    public static List<UiText> Texts(string addonName) =>
        AllTexts(addonName).FindAll(t => t.LabelVisible && t.TextVisible && !string.IsNullOrWhiteSpace(t.Text));

    // Every text node, hidden and empty ones included, so a word the boss hides or blanks keeps its slot.
    public static List<UiText> AllTexts(string addonName)
    {
        var result = new List<UiText>();
        var stage = AtkStage.Instance();
        if (stage == null) return result;

        // Some minigame addons can be open more than once, so walk every instance with this name.
        ref var list = ref stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
        for (var i = 0; i < Math.Min((int)list.Count,1024); i++)
        {
            var unit = list.Entries[i].Value;
            if (unit == null || !unit->IsVisible || unit->NameString != addonName) continue;
            Walk(addonName, &unit->UldManager, 0, result, LabelTint.None);
        }
        return result;
    }

    // One cast bar per casting enemy: its name (text #4) and how full it is. The fill is image #6,
    // 128 px wide when full. Node layout learned from XeldarAlz/FFXIV-AutoKeyboundBrawler.
    public readonly record struct CastBar(string Name, float Progress);

    private const uint CastNameNode = 4, CastFillNode = 6;
    private const float CastFillFullWidth = 128f;

    public static List<CastBar> CastBars(string addonName)
    {
        var result = new List<CastBar>();
        var stage = AtkStage.Instance();
        if (stage == null) return result;

        ref var list = ref stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
        for (var i = 0; i < Math.Min((int)list.Count,1024); i++)
        {
            var unit = list.Entries[i].Value;
            if (unit == null || !unit->IsVisible || unit->NameString != addonName) continue;
            ReadCastBar(&unit->UldManager, result);
            for (var n = 0; n < unit->UldManager.NodeListCount; n++)
            {
                var node = unit->UldManager.NodeList[n];
                if (node == null || (ushort)node->Type < 1000 || !node->IsVisible()) continue;
                var component = ((AtkComponentNode*)node)->Component;
                if (component != null) ReadCastBar(&component->UldManager, result);
            }
        }
        return result;
    }

    private static void ReadCastBar(AtkUldManager* uld, List<CastBar> result)
    {
        AtkResNode* fill = null;
        AtkTextNode* name = null;
        for (var i = 0; i < Math.Min((int)uld->NodeListCount,1024); i++)
        {
            var node = uld->NodeList[i];
            if (node == null) continue;
            if (node->NodeId == CastFillNode && node->Type == NodeType.Image) fill = node;
            else if (node->NodeId == CastNameNode && node->Type == NodeType.Text) name = (AtkTextNode*)node;
        }
        if (fill == null || name == null || !fill->IsVisible()) return;
        var progress = Math.Clamp(fill->Width * fill->ScaleX / CastFillFullWidth, 0f, 1f);
        result.Add(new CastBar(name->NodeText.ToString(), progress));
    }

    // Tints seen in match dumps: blue = plain enemy, gray = matches what is typed, red = "1000 Needles",
    // gold = other casts (Germinate, Tankbuster, Self-destruct), green = barrier phrase.
    private static readonly (LabelTint Tint, int R, int G, int B)[] KnownTints =
    [
        (LabelTint.Blue, -255, -255, 0),
        (LabelTint.Gray, -160, -160, -160),
        (LabelTint.Red, -128, -255, -255),
        (LabelTint.Gold, -200, -200, -255),
        (LabelTint.Green, -255, -200, -255),
    ];

    private const uint LabelBackgroundNode = 26;

    private static LabelTint TintOf(AtkUldManager* uld)
    {
        for (var i = 0; i < Math.Min((int)uld->NodeListCount,1024); i++)
        {
            var node = uld->NodeList[i];
            if (node == null || node->NodeId != LabelBackgroundNode) continue;
            var best = LabelTint.None;
            var bestDistance = int.MaxValue;
            foreach (var k in KnownTints)
            {
                var d = Math.Abs(node->AddRed - k.R) + Math.Abs(node->AddGreen - k.G) + Math.Abs(node->AddBlue - k.B);
                if (d < bestDistance) (best, bestDistance) = (k.Tint, d);
            }
            return bestDistance <= 60 ? best : LabelTint.None;
        }
        return LabelTint.None;
    }

    private static void Walk(string addon, AtkUldManager* uld, int depth, List<UiText> result, LabelTint tint, bool obscured = false)
    {
        if (depth > 8) return;
        for (var i = 0; i < Math.Min((int)uld->NodeListCount,1024); i++)
        {
            var node = uld->NodeList[i];
            if (node == null) continue;

            if (node->Type == NodeType.Text)
            {
                var text = (AtkTextNode*)node;
                var c = text->TextColor;
                result.Add(new UiText(addon, node->NodeId, text->NodeText.ToString(), node->ScreenX, node->ScreenY, text->FontSize,
                    (uint)(c.R << 24 | c.G << 16 | c.B << 8 | c.A), depth,
                    (nint)node, IsReallyVisible(node->ParentNode), node->IsVisible() && node->Alpha_2 != 0, tint, obscured));
            }
            else if ((ushort)node->Type >= 1000)
            {
                var component = ((AtkComponentNode*)node)->Component;
                if (component != null)
                    Walk(addon, &component->UldManager, depth + 1, result, TintOf(&component->UldManager), IsObscured(&component->UldManager));
            }
        }
    }

    // Cryptic Communications puts a noise overlay (#10) on a word label and hides the label, but the
    // word stays in its text node and can still be typed. A label hidden without the overlay is an
    // enemy that is gone. Overlay node learned from XeldarAlz/FFXIV-AutoKeyboundBrawler.
    private const uint ObscuringOverlayNode = 10;

    private static bool IsObscured(AtkUldManager* uld)
    {
        for (var i = 0; i < Math.Min((int)uld->NodeListCount,1024); i++)
        {
            var node = uld->NodeList[i];
            if (node != null && node->NodeId == ObscuringOverlayNode && (ushort)node->Type < 1000)
                return IsReallyVisible(node);
        }
        return false;
    }

    private static bool IsReallyVisible(AtkResNode* node)
    {
        for (var n = node; n != null; n = n->ParentNode)
            if (!n->IsVisible() || n->Alpha_2 == 0) return false;
        return true;
    }
}

