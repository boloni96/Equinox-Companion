using System.Buffers.Binary;
using System.Security.Cryptography;

namespace EquinoxCompanion;

public sealed record MarketHistorySale(uint Item,bool Hq,uint Quantity,long Net,DateTimeOffset At,string BuyerKey,int Occurrence);
public static class MarketRetainerHistory
{
    // RetainerHistory packet: 8-byte header, up to 20 52-byte records.
    // Layout cross-checked against CashFlow and AscendedLedger. The price
    // field is the received after-tax amount, not a listing unit price.
    public const int PacketSize=8+20*52;
    public static MarketHistorySale[] Read(ReadOnlySpan<byte> packet,DateTimeOffset now)=>Read(packet,now,out _);
    public static MarketHistorySale[] Read(ReadOnlySpan<byte> packet,DateTimeOffset now,out string error)
    {
        error="";
        if(packet.Length!=PacketSize){error="Unexpected history packet size.";return [];}
        var result=new List<MarketHistorySale>();var duplicates=new Dictionary<string,int>();
        for(var i=0;i<20;i++){
            var row=packet.Slice(8+i*52,52);
            var item=BinaryPrimitives.ReadUInt32LittleEndian(row);if(item==0)break;
            var net=BinaryPrimitives.ReadUInt32LittleEndian(row[4..]);
            var at=DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32LittleEndian(row[8..]));
            var quantity=BinaryPrimitives.ReadUInt32LittleEndian(row[12..]);
            if(item>=1_000_000||net==0||quantity is 0 or >9999||net>(long)SellerPricing.MaxPrice*quantity||row[16]>1||row[18]>1||at.Year<2013||at>now.AddMinutes(1)){error=$"History row {i+1} did not pass validation; existing records kept.";return [];}
            if(row[18]!=0)continue; // Mannequin sets cannot be assigned to one item.
            var buyer=row.Slice(19,32);var end=buyer.IndexOf((byte)0);if(end<0){error=$"History row {i+1} has an unexpected buyer field; existing records kept.";return [];}
            var key=Convert.ToHexString(SHA256.HashData(buyer[..end]));
            var identity=$"{item}:{row[16]}:{quantity}:{net}:{at.ToUnixTimeSeconds()}:{key}";
            var occurrence=duplicates.GetValueOrDefault(identity);duplicates[identity]=occurrence+1;
            result.Add(new(item,row[16]!=0,quantity,net,at,key,occurrence));
        }
        return result.ToArray();
    }
    public static MarketIncome[] Merge(MarketIncome[] existing,MarketRetainer retainer,MarketHistorySale[] history,Func<uint,string> name)
    {
        var result=existing.ToList();var claimed=new HashSet<int>();
        foreach(var sale in history){
            var id=$"history:{retainer.CharacterId}:{retainer.Id}:{sale.Item}:{sale.Hq}:{sale.Quantity}:{sale.Net}:{sale.At.ToUnixTimeSeconds()}:{sale.BuyerKey}:{sale.Occurrence}";
            if(result.Any(x=>x.Id==id))continue;
            var row=new MarketIncome(id,retainer.CharacterId,retainer.Character,retainer.WorldId,retainer.World,retainer.Id,retainer.Name,sale.Item,name(sale.Item),sale.Hq,sale.Quantity,null,sale.Net,sale.At,"Game retainer sale history; net and sale time confirmed");
            var match=-1;
            for(var i=0;i<result.Count;i++){
                var old=result[i];
                if(claimed.Contains(i)||old.Id.StartsWith("history:")||old.CharacterId!=retainer.CharacterId||(old.RetainerId!=0&&old.RetainerId!=retainer.Id)||old.ItemId!=sale.Item||old.Hq!=sale.Hq)continue;
                if(old.Evidence.StartsWith("Game sale message")&&old.Net==sale.Net&&(old.Quantity==0||old.Quantity==sale.Quantity)&&Math.Abs((old.At-sale.At).TotalSeconds)<=90){match=i;break;}
                const string marker="since ";
                var start=old.Evidence.IndexOf(marker,StringComparison.Ordinal);var end=old.Evidence.IndexOf(';',Math.Max(0,start));
                if(old.Id.StartsWith("inferred:")&&old.Quantity==sale.Quantity&&start>=0&&end>start&&DateTimeOffset.TryParse(old.Evidence[(start+marker.Length)..end],out var since)&&sale.At>=since&&sale.At<=old.At){match=i;break;}
            }
            if(match>=0){row=row with {Gross=result[match].Gross};result[match]=row;claimed.Add(match);}else result.Add(row);
        }
        return result.ToArray();
    }
}
