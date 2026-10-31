using EquinoxCompanion;
static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
Check(KeyboundPolicy.CanSubmit("Fight On!","Fight On!","Fight On!"),"spaces and punctuation preserved");
Check(!KeyboundPolicy.CanSubmit("new","old","old"),"replaced target cannot be submitted");
Check(!KeyboundPolicy.CanSubmit("word","wor","word"),"partial input cannot be submitted");
Check(!KeyboundPolicy.CanSubmit("","",""),"empty input cannot be submitted");
Check(!KeyboundPolicy.OwnsInput("my message","wor"),"unrelated input is not owned");
Check(!KeyboundPolicy.IsPhrase("???")&&!KeyboundPolicy.IsPhrase("wo*d")&&!KeyboundPolicy.IsPhrase("hello\nchat"),"masked and control text rejected");
Check(KeyboundPolicy.IsPhrase("Chocobo Square"),"multiword phrase accepted");

Check(KeyboundPolicy.ShouldRecall("","Cura","Cura",false),"consecutive repeat recalls");
Check(!KeyboundPolicy.ShouldRecall("","Cura","Phoenix",false),"different phrase types normally");
Check(!KeyboundPolicy.ShouldRecall("Cu","Cura","Cura",false),"partial text cannot recall");
Check(!KeyboundPolicy.ShouldRecall("","Cura","Cura",true),"recall attempted only once");
Check(!KeyboundPolicy.CanSubmit("Cura","Phoenix","Cura"),"wrong recalled history never submitted");

Check(KeyboundPolicy.CanRetryInput("Cu","Cu"),"lost key retries from last observed prefix");
Check(KeyboundPolicy.CanRetryInput("","Cu"),"game cleared buffer restarts typing");
Check(!KeyboundPolicy.CanRetryInput("Other","Cu"),"unrelated text cannot be adopted for retry");
Check(!KeyboundPolicy.CanSubmit("Cura","","Cura"),"cleared buffer never submits");

Check(KeyboundPolicy.AbandonMissingTarget(false,"Cur","Cu","Cur"),"vanished target cancels in-flight character acknowledgement");
Check(KeyboundPolicy.AbandonMissingTarget(false,"Cu","Cu","Cur"),"vanished target cancels lost character wait");
Check(!KeyboundPolicy.AbandonMissingTarget(true,"Cu","Cu","Cur"),"live target keeps acknowledgement wait");
Check(!KeyboundPolicy.AbandonMissingTarget(false,"user text","Cu","Cur"),"vanished target cannot erase unrelated text");

Check(KeyboundPolicy.TypingDelay(60,100,0)==60&&KeyboundPolicy.TypingDelay(60,100,1)==100,"configured delay bounds");
Check(KeyboundPolicy.TypingDelay(0,-10,0.5)==10,"invalid saved delay clamped");
Check(KeyboundPolicy.TypingDelay(200,100,0.5)==150,"inverted delay bounds normalized");
Check(KeyboundPolicy.LoopTerritory(1359)&&KeyboundPolicy.LoopTerritory(1360)&&!KeyboundPolicy.LoopTerritory(1),"repeat limited to event territories");

Check(KeyboundPolicy.SoftMinMs==80&&KeyboundPolicy.SoftMaxMs==150&&KeyboundPolicy.SoftSubmitMs==80,"Soft preset matches requested timings");
Check(!KeyboundPolicy.LiveWord(false,true,true,true),"hidden component cannot retain stale live word even with leftover noise overlay");
Check(KeyboundPolicy.LiveWord(true,false,false,true),"visible component with obscuring overlay remains live");
Check(!KeyboundPolicy.LiveWord(true,false,false,false),"disappeared label is not live");
Check(KeyboundPolicy.LiveWord(true,true,true,false),"normal visible word remains live");
Check(KeyboundPolicy.CanClear(true,"Cur","Cur"),"owned nonempty active input can use Escape clear");
Check(!KeyboundPolicy.CanClear(true,"",""),"never Escape an empty input and quit");
Check(!KeyboundPolicy.CanClear(false,"Cur","Cur"),"never Escape inactive input");
Check(!KeyboundPolicy.CanClear(true,"my text","Cur"),"never clear someone else's text");
Check(KeyboundPolicy.TypingDelay(200,100,0)==100&&KeyboundPolicy.TypingDelay(200,100,1)==200,"independent crossed sliders produce bounded sorted timing");

Check(KeyboundPolicy.CanRecoverInput("Cu","Cu","Cur",true),"interrupted pending key can clear its observed buffer");
Check(KeyboundPolicy.CanRecoverInput("C","Cu","Cur",true),"game-truncated pending prefix can recover");
Check(KeyboundPolicy.CanRecoverInput("Curr","Cu","Cur",true),"one duplicated outstanding character can recover");
Check(!KeyboundPolicy.CanRecoverInput("Curx","Cu","Cur",true),"arbitrary appended user text cannot be cleared");
Check(!KeyboundPolicy.CanRecoverInput("Other","Cu","Cur",true),"unrelated text is preserved during recovery");
Check(!KeyboundPolicy.CanRecoverInput("C","Cu","Cur",false),"stale expected text does not grant ownership");
Check(KeyboundPolicy.CanRecoverInput("Cura","Cura","",true),"interrupted submission can clear owned complete word");
Check(KeyboundPolicy.CanRecoverInput("","","Cur",true),"game-cleared field recovers without Escape");
Check(!KeyboundPolicy.CanRecoverInput("Phoenix","","Cura",true),"unexpected history phrase is preserved");

Check(KeyboundShoppingPolicy.Total(null)==6380,"official twenty-item total");
Check(KeyboundShoppingPolicy.Total(new Dictionary<string,int>())==0,"empty custom plan excludes every reward");
var plan=new Dictionary<string,int>{{"scorpion",1},{"coffer",3},{"not-a-prize",999}};
Check(KeyboundShoppingPolicy.Total(plan)==3160,"selected quantities and unknown keys");
plan["scorpion"]=0;
Check(KeyboundShoppingPolicy.Total(plan)==2730,"owned reward excluded");
Check(KeyboundShoppingPolicy.Remaining(6380,1281)==5099,"live wallet reduces required earnings");
Check(KeyboundShoppingPolicy.Remaining(430,600)==0,"surplus balance never produces negative target");
Check(KeyboundShoppingPolicy.Rounds(5099,new uint[]{331,340})==16,"round estimate rounds up actual average");
Check(KeyboundShoppingPolicy.Rounds(100,Array.Empty<uint>()) is null,"unknown earnings do not invent an estimate");
Check(KeyboundShoppingPolicy.Rounds(100,new uint[]{0}) is null,"zero rewards do not divide by zero");
var earnings=new KeyboundRoundEarnings();
Check(earnings.Observe(true,false,950,10000) is null,"round start does not count");
Check(earnings.Observe(true,true,1281,10000) is null,"results wait for round exit");
Check(earnings.Observe(true,true,1281,10000) is null,"repeated results polls do not count again");
Check(earnings.Observe(false,false,1281,10000)==331,"completed observed round yields actual earnings");
Check(earnings.Observe(false,false,1281,10000) is null,"round counted once");
earnings.Observe(true,true,1600,10000);
Check(earnings.Observe(false,false,1600,10000) is null,"joining at results cannot invent baseline");
earnings.Observe(true,false,9900,10000);earnings.Observe(true,true,10000,10000);
Check(earnings.Observe(false,false,10000,10000) is null,"capacity-limited round excluded");
earnings.Observe(true,false,1500,10000);earnings.Observe(true,false,1000,10000);earnings.Observe(true,true,1331,10000);
Check(earnings.Observe(false,false,1331,10000) is null,"balance reduction invalidates baseline");

var wishes=KeyboundShoppingPolicy.WishlistPlan(new[]{("Wind-up Barret",true,false),("Magitek Scorpion Identification Key",true,true),("Keybound Coffer",false,false),("Unknown custom reward",true,false)},out var unknown);
Check(KeyboundShoppingPolicy.Total(wishes)==260&&unknown==1,"Journal wanted-not-obtained only, unknown costs flagged");
var duplicates=KeyboundShoppingPolicy.WishlistPlan(new[]{("Avalanche Brawler’s Top",true,false),("Avalanche Brawler's Top",true,false)},out unknown);
Check(KeyboundShoppingPolicy.Total(duplicates)==430&&unknown==0,"normalized apostrophe and duplicate wishlist entries");
Check(KeyboundShoppingPolicy.Total(KeyboundShoppingPolicy.WishlistPlan(Array.Empty<(string,bool,bool)>(),out unknown))==0,"empty synced wishlist never selects everything");

Check(KeyboundPolicy.IsPhrase("Worth the Weight?"),"wave two boss question phrase is accepted");
Check(KeyboundPolicy.CanSubmit("Worth the Weight?","Worth the Weight?","Worth the Weight?"),"boss question mark is preserved on submission");
Check(!KeyboundPolicy.CanSubmit("Worth the Weight?","Worth the Weight","Worth the Weight?"),"boss phrase requires its final punctuation");
Check(!KeyboundPolicy.IsPhrase("???")&&!KeyboundPolicy.IsPhrase("***"),"placeholder-only labels stay excluded");

foreach(var phrase in new[]{"Fight On!","Worth the Weight?","Wait.","Really?!","Ready..."})
{
    Check(KeyboundPolicy.IsPhrase(phrase),"punctuation accepted: "+phrase);
    Check(KeyboundPolicy.CanSubmit(phrase,phrase,phrase),"punctuation submitted unchanged: "+phrase);
}


Check(KeyboundBuyingPolicy.ExactCost(52322,430,430,1,1),"exact one-item MGC exchange");
Check(!KeyboundBuyingPolicy.ExactCost(1,430,430,1,1),"reject wrong currency");
Check(!KeyboundBuyingPolicy.ExactCost(52322,431,430,1,1),"reject changed price");
Check(!KeyboundBuyingPolicy.ExactCost(52322,430,430,2,1),"reject multi-item bundle");
Check(!KeyboundBuyingPolicy.ExactCost(52322,430,430,1,2),"reject duplicate quantity");
var intent=new KeyboundPurchaseReceipt(12,123,430,1000,0);
Check(KeyboundBuyingPolicy.Confirmed(intent,570,1),"purchase requires exact charge and item receipt");
Check(!KeyboundBuyingPolicy.Confirmed(intent,570,0),"charge alone is not receipt");
Check(!KeyboundBuyingPolicy.Confirmed(intent,1000,1),"item alone is not receipt");
Check(!KeyboundBuyingPolicy.Confirmed(intent,569,1),"unrelated currency change rejected");
Check(KeyboundBuyingPolicy.CategoryMatches("Weapons","MGC Exchange (Weapons)"),"native category label suffix");
Check(!KeyboundBuyingPolicy.CategoryMatches("Weapons","Armor"),"unrelated category rejected");
Check(!KeyboundBuyingPolicy.CategoryMatches("",""),"empty menu rejected");
var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
try{
    var file=Path.Combine(directory,"purchases.json");var book=new KeyboundPurchaseBook(file);
    book.Put(intent);var reload=new KeyboundPurchaseBook(file);
    Check(reload.Find(12,123) is {Confirmed:false},"pending intent survives restart to prevent blind retry");
    Check(reload.Find(13,123) is null,"another character has independent ownership");
    reload.Put(intent with {Confirmed=true});
    Check(new KeyboundPurchaseBook(file).Find(12,123) is {Confirmed:true},"confirmed receipt survives restart");
    Check(new KeyboundPurchaseBook(file).Entries.Count==1,"receipt transition does not create duplicate entries");
    File.WriteAllText(file,"broken");var rejected=false;try{_ =new KeyboundPurchaseBook(file);}catch{rejected=true;}
    Check(rejected,"corrupt history blocks purchases rather than treating them as unowned");
}finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
