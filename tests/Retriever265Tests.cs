using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Retriever265Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var recipes=new Dictionary<uint,RetrievalRecipe>{{10,new(10,1,3,[],"A")},{11,new(11,1,3,[],"B")}};
        var names=new Dictionary<uint,string>{{1,"Ingot"}};
        void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS retriever 265 "+label);}
        var read=RetainerRetrieval.ReadArtisan("""
        {"NewCraftingLists":[
          {"Name":"Multi yield","Recipes":[{"ID":10,"Quantity":4,"ListItemOptions":null}]},
          {"Name":"Old recipe","Recipes":[{"ID":999,"Quantity":1}]},
          {"Name":"Two jobs","Recipes":[{"ID":10,"Quantity":1},{"ID":11,"Quantity":2}]},
          {"Name":"Skipped","Recipes":[{"ID":999,"Quantity":1,"ListItemOptions":{"Skipping":true}}]}
        ]}
        """,recipes,names);
        Check("one bad list does not hide valid lists",read.Lists.Length==3&&read.Warnings.Length==1&&read.Warnings[0].Contains("Old recipe"));
        Check("craft counts convert to output quantities",read.Lists[0].Items.Single().Quantity==12);
        Check("same output across jobs combines without hiding list",read.Lists[1].Items.Single().Quantity==9&&read.Lists[1].Items.Single().RecipeId==0);
        Check("skipped unknown recipes ignored",read.Lists[2].Items.Length==0);
        read=RetainerRetrieval.ReadArtisan("""{"Payload":{"NewCraftingLists":{"$values":[{"Name":"Wrapped","Recipes":{"$values":[{"ID":10,"Quantity":2}]}}]}}} """,recipes,names);
        Check("wrapped arrays read",read.Lists.Single().Items.Single().Quantity==6);
        read=RetainerRetrieval.ReadArtisan("""[{"Name":"Premade","Recipes":[{"ID":10,"Quantity":2}]}]""",recipes,names);
        Check("premade cache root array reads",read.Lists.Single().Items.Single().Quantity==6);
        var preview=new RetrievalPreview(1,"Ingot",600,200,300);
        Check("preview separates required held fetch and missing",preview.Need==400&&preview.Missing==100);
        Check("unknown stock is not zero",(preview with{Available=null}).Missing==null);
    }
}
