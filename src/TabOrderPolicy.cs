namespace EquinoxCompanion;
public static class TabOrderPolicy
{
    public static List<string> Reconcile(IEnumerable<string> saved,IEnumerable<string> available,bool reset)
    {
        var ids=available.Distinct().ToList();var order=saved.Where(x=>x!="tests").Distinct().ToList();
        if(reset)
        {
            var people=order.Concat(ids).Where(x=>x.StartsWith("person:")).Distinct().ToList();
            order=["housing",..people,"submarines","settings"];
        }
        foreach(var id in ids.Where(id=>!order.Contains(id)))
        {
            var before=id.StartsWith("person:")||id=="planting"?order.IndexOf("submarines"):order.IndexOf("settings");
            if(before>=0)order.Insert(before,id);else order.Add(id);
        }
        order.RemoveAll(x=>x=="settings");order.Add("settings");
        return order;
    }
}
