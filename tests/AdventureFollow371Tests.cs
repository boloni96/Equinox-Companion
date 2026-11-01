using EquinoxCompanion;
public static class AdventureFollow371Tests
{
 public static void Run()
 {
  void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS local follow: "+name);}
  AdventureStop Stop(string name,string world,string cid,bool done=false)=>new(name,"","",0,[new(name,"","",done,true)],world,[cid]);
  var board=new AdventureBoard("multi","g1","","multi","Route",[Stop("Other","Rafflesia","other"),Stop("Me","Rafflesia","me"),Stop("Away","Sophia","me")],0,1);
  Check(AdventureFollowSelection.Index(board,"me","Rafflesia","")==1,"follow selects local character without server page change");
  Check(AdventureFollowSelection.Index(board,"me","Sophia","")==2,"follow selects current world");
  Check(AdventureFollowSelection.Index(board,"unknown","Rafflesia","")==null,"missing character does not select someone else");
  Check(AdventureFollowSelection.Index(board,"","Rafflesia","")==null,"unresolved roster does not select someone else");
  var done=board with {Stops=[board.Stops[0],Stop("Me","Rafflesia","me",true),board.Stops[2]]};
  Check(AdventureFollowSelection.Index(done,"me","Rafflesia","")==1,"completed character still follows");
  Check(AdventureFollowSelection.Index(board with {Stops=[board.Stops[0],board.Stops[1]]},"me","Sophia","")==null,"wait for travel grouping to add new stop");
  var local=new AdventureViewState();local.Move(board,0);
  var regrouped=board with {Index=0,Revision=2,Stops=[board.Stops[0],board.Stops[2],board.Stops[1]]};
  var next=AdventureFollowSelection.Index(local.Apply(regrouped),"me","Sophia","");
  Check(next==1&&local.Move(regrouped,next!.Value).Index==1,"travel response selects matching local stop instead of shared index");
  var garden=board with {Kind="garden",Stops=[board.Stops[0] with {HouseId="a",Batch=1},board.Stops[1] with {HouseId="b",Batch=1}]};
  Check(AdventureFollowSelection.Index(garden,"visitor","Rafflesia","b")==1,"house follow works for a visitor");
  var second=new AdventureViewState();second.Move(board,0);local.Move(board,2);
  Check(second.Apply(board with {Revision=3,Index=2}).Index==0,"following one client leaves the other cursor alone");
 }
}
