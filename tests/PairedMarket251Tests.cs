using EquinoxCompanion;
static class PairedMarket251Tests
{
 public static void Run(){
  void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
  var now=DateTimeOffset.UtcNow;var old=now.AddHours(-2);
  var stock=new MarketStock(0,10,"Item",false,2,500,old);
  var local=new MarketRetainer(123,"Retainer",999,"Character",57,"Siren","Old person","MAIN",1234,now,[stock],old,0,JobId:16,Level:70,Job:"MIN",ProfileAt:old);
  var row=new RetainerMarketRow("123","Retainer",1,1234,old,now,[],16,80,"MIN",now);
  var c=new SharedCharacter("c1","Character","Siren","","","MAIN",[],AccountId:"a1",ContentId:"999",RetainerMarket:new([row]));
  var roster=new SharedRoster(1,now,[new("p1","Person",[c])],MarketProjectionVersion:1);
  MarketRetainer[] View(MarketRetainer[] l,SharedRoster? r)=>PairedMarketView.Build(l,r,w=>w=="Siren"?57u:0,now);
  var v=View([local],roster).Single();
  Check("paired profile refresh preserves newer stock observation",v.Stock.Length==1&&v.At==now&&v.Level==80&&v.ProfileAt==now&&v.LastVisit==now&&v.DisplayOrder==1);
  Check("shared identity follows stable person and account IDs",v.Person=="Person"&&v.PersonId=="p1"&&v.AccountId=="a1");
  var cleared=roster with {People=[new("p1","Person",[c with {RetainerMarket=new([row with {ObservedAt=now.AddSeconds(1)}])}])]};
  Check("observed empty paired stock clears old local selling view",View([local],cleared).Single().Stock.Length==0);
  Check("legacy server does not invent paired data",View([],roster with {MarketProjectionVersion=0}).Length==0);
  Check("disabling pairing removes remote view without mutating local",View([local],null).Single()==local&&local.Stock.Length==1);
  Check("unknown world and malformed rows excluded",PairedMarketView.Build([],roster,_=>0,now).Length==0&&View([],roster with {People=[new("p1","Person",[c with {RetainerMarket=new([row with {Level=-1}])}])]}).Length==0);
  var other=c with {Id="c2",ContentId="888",Name="Other",AccountId="a2"};
  Check("conflicting retainer identity excluded",View([local],roster with {People=[new("p2","Other",[other])]}).Length==0);
  Check("same account labels have separate scopes",PairedMarketView.AccountKey(v)!=PairedMarketView.AccountKey(v with {PersonId="p2"}));
  Check("unchanged projections keep semantic equality",PairedMarketView.Same([v],[v with {Stock=[..v.Stock]}])&&!PairedMarketView.Same([v],[v with {Level=81}]));
  var melded=row with {Items=[new(0,10,"Item",true,1,1197021,old,Materia:"219,219,234,235,250")]};
  var withMateria=roster with {People=[new("p1","Person",[c with {RetainerMarket=new([melded])}])]};
  Check("paired stock retains exact materia types and grades",View([],withMateria).Single().Stock.Single().Materia=="219,219,234,235,250");
  Check("bad shared materia excluded",!RetainerMarketSnapshot.Valid(new([melded with {Items=[melded.Items[0] with {Materia="250,219"}]}]),now));
  Check("outbox keeps plain distinct from unknown",RetainerMarketSnapshot.Valid(new([melded with {Items=[melded.Items[0] with {Materia=""}]}]),now)&&RetainerMarketSnapshot.Valid(new([melded with {Items=[melded.Items[0] with {Materia=null}]}]),now));
  var priced=melded with {PriceReview=new(MarketPriceReviews.Key([new(0,10,"Item",true,1,1197021,old,Materia:"219,219,234,235,250")]),now,now.AddMinutes(5),1,0,0,0,"Universalis")};
  var pricedRoster=roster with {People=[new("p1","Person",[c with {RetainerMarket=new([priced])}])]};
  Check("paired price summary survives projection",View([],pricedRoster).Single().PriceReview?.Updates==1);
  var sameLocal=View([],pricedRoster).Single() with {PriceReview=null,At=now};
  Check("newer stock timestamp does not lose matching price summary",View([sameLocal],pricedRoster).Single().PriceReview?.Updates==1);
  Check("changed stock drops incompatible shared price summary",View([sameLocal with {Stock=[sameLocal.Stock[0] with {Price=1}]}],pricedRoster).Single().PriceReview==null);
  var source=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../src/GilTraderPanel.cs"));
  var text=File.ReadAllText(source);
  Check("send and receive actions have distinct ImGui IDs from direction controls",text.Contains("Send##gil-mode-send")&&text.Contains("Receive##gil-mode-receive")&&text.Contains("Send gil###gil-start")&&text.Contains("Send items###gil-start")&&!text.Contains("ImGui.Button(\"Send gil\")"));
 }
}
