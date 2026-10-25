using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace EquinoxCompanion;

public static class MarketSalesImport
{
    public static MarketIncome[] Read(TextReader source)
    {
        using var reader=new TextFieldParser(source){HasFieldsEnclosedInQuotes=true};
        reader.SetDelimiters(",");
        var header=reader.ReadFields()??[];
        string[] expected=["Retainer ID","World ID","Item ID","Is HQ?","Quantity","Unit Price","Tax Rate","Sold At"];
        if(!expected.All(header.Contains)||header.Distinct().Count()!=header.Length)
            throw new IOException("Choose the Allagan Market Sale History CSV export.");
        var columns=header.Select((name,index)=>(name,index)).ToDictionary(x=>x.name,x=>x.index);
        var occurrences=new Dictionary<string,int>();
        var rows=new List<MarketIncome>();
        while(!reader.EndOfData){
            var fields=reader.ReadFields()??[];
            if(fields.Length!=header.Length)throw new IOException("CSV row does not match its header.");
            string Field(string n)=>fields[columns[n]];
            var retainer=ulong.Parse(Field("Retainer ID"),CultureInfo.InvariantCulture);
            var world=uint.Parse(Field("World ID"),CultureInfo.InvariantCulture);
            var item=uint.Parse(Field("Item ID"),CultureInfo.InvariantCulture);
            var quantity=uint.Parse(Field("Quantity"),CultureInfo.InvariantCulture);
            var price=long.Parse(Field("Unit Price"),CultureInfo.InvariantCulture);
            if(Field("Is HQ?") is not ("0" or "1"))throw new IOException("CSV contains an invalid quality flag.");
            var hq=Field("Is HQ?")=="1";
            var at=DateTimeOffset.Parse(Field("Sold At"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal);
            if(retainer==0||world==0||item==0||quantity is 0 or >9999||price is <=0 or >SellerPricing.MaxPrice)
                throw new IOException("CSV contains an invalid sale.");
            var key="allagan:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{retainer}/{world}/{item}/{hq}/{quantity}/{price}/{at:O}")));
            // Multiple identical sales in the same second are real rows, not
            // duplicates. Their occurrence number remains stable on re-import.
            var occurrence=occurrences.GetValueOrDefault(key)+1;occurrences[key]=occurrence;
            if(occurrence>1)key+="/"+occurrence;
            // Historical Tax Rate exports are not sufficient to verify net gil.
            rows.Add(new(key,0,"",world,"",retainer,"",item,"",hq,quantity,price*quantity,null,at,"Imported Allagan Market history; net not verified"));
            if(rows.Count>100000)throw new IOException("Import at most 100,000 rows at once.");
        }
        return rows.ToArray();
    }
}
