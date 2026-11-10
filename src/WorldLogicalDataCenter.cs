using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;

internal static class WorldLogicalDataCenter
{
    private static readonly Dictionary<uint,WorldDCGroupType> cache=[];
    // EXDSchema World: 7.58 adds LogicalDataCenter at column 3 (9 columns).
    // The prior 8-column schema has the logical DC at column 2. Read column
    // metadata rather than memory offsets, independently of generated bindings.
    internal static WorldDCGroupType LogicalDc(this World world)
    {
        lock(cache){
            if(cache.TryGetValue(world.RowId,out var known))return known;
            var raw=Plugin.DataManager.GetExcelSheet<RawRow>(name:"World").GetRow(world.RowId);
            var column=raw.Columns.Count switch {9=>3,8=>2,_=>throw new InvalidOperationException("Unrecognized World data layout; data-center selection stopped.")};
            if(raw.ReadColumn(1).ToString()!=world.Name.ToString())
                throw new InvalidOperationException("World data identity mismatch; data-center selection stopped.");
            var id=Convert.ToUInt32(raw.ReadColumn(column));
            var dc=Plugin.DataManager.GetExcelSheet<WorldDCGroupType>().GetRow(id);
            if(world.IsPublic&&(id==0||dc.Region.RowId is <1 or >4||string.IsNullOrWhiteSpace(dc.Name.ToString())))
                throw new InvalidOperationException("Logical data center is unavailable for "+world.Name.ToString());
            return cache[world.RowId]=dc;
        }
    }
}
