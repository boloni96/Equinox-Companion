namespace EquinoxCompanion;
public static class GardenObjectDiscovery
{
    public static (int Plot,int Patch,int Bed)? Decode(uint argument)
    {
        var plot=(int)(argument>>8&255)+1;var patch=(int)(argument>>16&255)+1;var bed=(int)(argument>>24)+1;
        return plot is >=1 and <=60&&patch is >=1 and <=3&&bed is >=1 and <=8?(plot,patch,bed):null;
    }
    // Require a complete, co-located set; never infer identity from distance alone.
    // Existing manual mappings and batch labels take precedence.
    public static SharedGardenTarget[] Missing(Address address,IEnumerable<GardenTargetDetails> objects,IEnumerable<SharedGardenTarget> saved,DateTimeOffset now)
    {
        if(address.Apartment||address.Workshop||address.Room!=0||address.Plot is <1 or >60)return [];
        var known=saved.Where(m=>m.GameHouseId==address.HouseId&&m.World==(address.WorldName??"")&&m.Ward==address.Ward&&m.Plot==address.Plot)
            .GroupBy(m=>m.Argument).ToDictionary(g=>g.Key,g=>g.MaxBy(m=>m.At)!);
        var result=new List<SharedGardenTarget>();
        var groups=objects.Where(t=>Decode(t.Argument) is {} d&&d.Plot==address.Plot)
            .GroupBy(t=>t.Argument&0xFFFFFF).ToArray();
        if(groups.Length>3)return [];
        foreach(var group in groups){
            var beds=group.ToArray();if(beds.Length!=8||beds.Select(t=>Decode(t.Argument)!.Value.Bed).Distinct().Count()!=8)continue;
            var first=beds[0];
            if(beds.Any(t=>!float.IsFinite(t.X)||!float.IsFinite(t.Y)||!float.IsFinite(t.Z)||
                Math.Pow(t.X-first.X,2)+Math.Pow(t.Y-first.Y,2)+Math.Pow(t.Z-first.Z,2)>.25*.25))continue;
            var existing=beds.Where(t=>known.ContainsKey(t.Argument)).Select(t=>known[t.Argument]).ToArray();
            if(existing.Any(m=>Decode(m.Argument)!.Value.Bed!=m.Bed)||existing.Select(m=>m.Patch).Distinct().Count()>1)continue;
            var patch=existing.Length>0?existing[0].Patch:Decode(first.Argument)!.Value.Patch;
            if(patch is <1 or >3)continue;
            foreach(var t in beds.Where(t=>!known.ContainsKey(t.Argument)))
                result.Add(new(address.HouseId,address.WorldName??"",address.DistrictName??"",address.Ward,address.Plot,patch,Decode(t.Argument)!.Value.Bed,t.Argument,t.X,t.Y,t.Z,now));
        }
        return result.ToArray();
    }
}
