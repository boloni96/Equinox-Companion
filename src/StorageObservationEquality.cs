namespace EquinoxCompanion;

internal static class StorageObservationEquality
{
    internal static bool Same(StorageDetails a, StorageDetails b)
    {
        if(a.Key != b.Key || a.Name != b.Name || !a.Items.AsSpan().SequenceEqual(b.Items)) return false;
        if(a.Stacks is null || b.Stacks is null) return a.Stacks is null && b.Stacks is null;
        if(a.Stacks.Length != b.Stacks.Length) return false;
        for(var i = 0; i < a.Stacks.Length; i++)
        {
            var x = a.Stacks[i]; var y = b.Stacks[i];
            if(x.ItemId != y.ItemId || x.Quantity != y.Quantity || x.Hq != y.Hq || x.Name != y.Name) return false;
            if(x.Positions is null || y.Positions is null)
            { if(x.Positions is not null || y.Positions is not null) return false; }
            else if(!x.Positions.AsSpan().SequenceEqual(y.Positions)) return false;
        }
        return true;
    }
}
