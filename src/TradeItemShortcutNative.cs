using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AtkType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace EquinoxCompanion;

// Deliberately uses the game's enabled Trade action. No item-transfer function,
// numeric prompt confirmation or trade acceptance is invoked by this feature.
internal static unsafe class TradeItemShortcutNative
{
    internal static int NativeEntry(AgentInventoryContext* agent, IReadOnlySet<string> labels)
    {
        if (agent == null || agent->ContextItemCount is < 1 or > 32 ||
            agent->ContexItemStartIndex < 0 ||
            agent->ContexItemStartIndex + agent->ContextItemCount > agent->EventParams.Length) return -1;
        var found = -1;
        for (var i = 0; i < agent->ContextItemCount; i++)
        {
            var value = agent->EventParams[agent->ContexItemStartIndex + i];
            if (value.Type is not (AtkType.String or AtkType.ManagedString or AtkType.ConstString) ||
                !labels.Contains(Read(value.String.Value))) continue;
            if (found >= 0 || (agent->ContextItemDisabledMask & (1u << i)) != 0 ||
                (agent->ContextItemSubmenuMask & (1u << i)) != 0) return -1;
            found = i;
        }
        return found;
    }

    internal static int DisplayedEntry(AtkValue* values, int valueCount, string label)
    {
        // Dalamud can prepend plugin entries. Never use the native entry's
        // index as the rendered callback index: it could now mean another action.
        if (values == null || valueCount < 9 || values[0].Type != AtkType.UInt ||
            values[0].UInt is < 1 or > 32 || string.IsNullOrEmpty(label)) return -1;
        var count = (int)values[0].UInt;
        if (valueCount != 8 + count && valueCount != 8 + count * 2) return -1;
        if (values[2].Type != AtkType.UInt || values[3].Type != AtkType.UInt) return -1;
        var found = -1;
        for (var i = 0; i < count; i++)
        {
            var value = values[8 + i];
            if (value.Type is not (AtkType.String or AtkType.ManagedString or AtkType.ConstString) ||
                !string.Equals(Read(value.String.Value), label, StringComparison.Ordinal)) continue;
            if (found >= 0 || ((values[2].UInt | values[3].UInt) & (1u << i)) != 0) return -1;
            if (valueCount == 8 + count * 2)
            {
                var disabled = values[8 + count + i];
                if (disabled.Type != AtkType.Int || disabled.Int != 0) return -1;
            }
            found = i;
        }
        return found;
    }

    internal static string Read(byte* text)
    {
        if (text == null) return "";
        var length = 0;
        while (length < 2048 && text[length] != 0) length++;
        // Keep the raw label so plugin prefixes/formatting cannot impersonate
        // a plain native label after stripping SeString payloads.
        return length == 2048 ? "" : System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(text, length));
    }
}

internal readonly record struct TradeItemShortcutTarget(
    ulong Character, uint Partner, nint TradeAddon, nint ItemPointer,
    uint Inventory, int Slot, uint ItemId, byte Flags, int Quantity);

internal sealed record TradeItemShortcutRequest(TradeItemShortcutTarget Target, string Label, long Created)
{
    internal bool Matches(TradeItemShortcutTarget current, long now) =>
        now >= Created && now - Created <= 750 && Target == current;
}
