namespace EquinoxCompanion;

// One instance per game client. The shared board carries progress, not this cursor.
public sealed class AdventureViewState
{
    private sealed record Cursor(string Generation,string Stop,int Index,bool Reviewed);
    private readonly Dictionary<string,Cursor> cursors=[];
    public void Clear()=>cursors.Clear();
    private static string StopKey(AdventureStop stop)=>System.Text.Json.JsonSerializer.Serialize(new {
        stop.HouseId,stop.Batch,stop.World,
        Characters=(stop.CharacterIds??[]).Order(StringComparer.Ordinal).ToArray(),
        Title=stop.HouseId.Length>0?"":stop.Title
    });
    public AdventureBoard Apply(AdventureBoard board)
    {
        if(!cursors.TryGetValue(board.Id,out var cursor)||cursor.Generation!=board.Generation)return Move(board,board.Index);
        var index=cursor.Reviewed?board.Stops.Length:cursor.Index<board.Stops.Length&&StopKey(board.Stops[cursor.Index])==cursor.Stop?cursor.Index:Array.FindIndex(board.Stops,s=>StopKey(s)==cursor.Stop);
        if(index<0)index=Math.Clamp(cursor.Index,0,Math.Max(0,board.Stops.Length-1));
        return Move(board,index);
    }
    public AdventureBoard Move(AdventureBoard board,int index)
    {
        index=Math.Clamp(index,0,board.Stops.Length);
        var reviewed=index==board.Stops.Length;
        cursors[board.Id]=new(board.Generation,reviewed?"":StopKey(board.Stops[index]),index,reviewed);
        return index==board.Index?board:board with {Index=index};
    }
}
