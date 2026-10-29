namespace EquinoxCompanion;

public static class CrystalStorage
{
    public static bool IsItem(uint item) => item is >= 2 and <= 19;
    public static bool IsContainer(uint container) => container is 2001 or 12001 or 22001;
    public static ChestStack Normalize(ChestStack stack)
    {
        if (!IsContainer(stack.Container)) return stack;
        if (stack.Slot is < 0 or >= 18) throw new InvalidOperationException("Unexpected crystal inventory layout.");
        var expected = (uint)stack.Slot + 2;
        if (stack.Item != expected && (stack.Item != 0 || stack.Quantity != 0))
            throw new InvalidOperationException("Crystal inventory identity changed; refresh before transferring.");
        return stack with { Item = expected, Hq = false, Limit = 9999 };
    }
    public static bool CanReceive(ChestStack source, ChestStack target) =>
        IsItem(source.Item)
            ? IsContainer(target.Container) && !source.Hq && target.Item == source.Item && target.Slot == (int)source.Item - 2
            : !IsContainer(target.Container);
    public static int Room(uint item, IReadOnlyList<ChestStack> slots) =>
        IsItem(item) ? slots.Where(s => IsContainer(s.Container) && s.Item == item && s.Slot == (int)item - 2).Sum(s => Math.Max(0, s.Limit - s.Quantity)) : 0;
}
