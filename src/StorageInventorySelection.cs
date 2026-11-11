namespace EquinoxCompanion;
// A recorded item selection belongs to its source location, even in a flattened view.
internal sealed class StorageInventorySelection
{
    private readonly HashSet<(string Location,uint Item)> selected=[];
    public bool Contains(string location,uint item)=>selected.Contains((location,item));
    public void Set(string location,uint item,bool value){if(value)selected.Add((location,item));else selected.Remove((location,item));}
    public IEnumerable<uint> Items(string location)=>selected.Where(x=>x.Location==location).Select(x=>x.Item);
    public void Clear(string location=""){if(location.Length==0)selected.Clear();else selected.RemoveWhere(x=>x.Location==location);}
}
