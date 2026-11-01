namespace EquinoxCompanion;
public static class AdventureFollowSelection
{
    public static int? Index(AdventureBoard board,string character,string world,string house)
    {
        var houseIndex=AdventurePolicy.HouseIndex(board,house,world);
        if(houseIndex is not null)return houseIndex;
        if(character.Length==0&&board.Kind!="garden")return null;
        var matches=board.Stops.Select((s,i)=>(Stop:s,Index:i)).Where(x=>string.Equals(x.Stop.World,world,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(character.Length>0&&board.Kind!="garden")matches=matches.Where(x=>x.Stop.CharacterIds?.Contains(character)==true).ToArray();
        if(matches.Length==0)return null;
        if(matches.Any(x=>x.Index==board.Index))return board.Index;
        var next=matches.FirstOrDefault(x=>!AdventurePolicy.Complete(x.Stop));
        return next.Stop is null?matches[0].Index:next.Index;
    }
}
