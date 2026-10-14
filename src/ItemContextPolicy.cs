namespace EquinoxCompanion;
internal static class ItemContextPolicy
{
    // Game item IDs may carry collectible (500,000) or HQ (1,000,000) flags.
    // Event items use a separate sheet and cannot be searched as normal items.
    internal static uint Normalize(ulong id) => id is > 0 and < 2_000_000 ? (uint)(id % 500_000) : 0;
}
