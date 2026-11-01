using EquinoxCompanion;
public static class AdventureNavigation370Tests
{
 public static void Run()
 {
  void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS client navigation: "+name);}
  AdventureStop Stop(string name)=>new(name,"",name,1,[new(name+"-task",name,"",false,true)],"Rafflesia");
  var board=new AdventureBoard("route","g1","","garden","Gardens",[Stop("House A"),Stop("House B"),Stop("House C")],0,1);
  var a=new AdventureViewState();var b=new AdventureViewState();
  a.Apply(board);b.Apply(board);
  var moved=a.Move(board,1);
  Check(moved.Index==1&&board.Index==0,"local move leaves shared board untouched");
  Check(b.Apply(board).Index==0,"second client stays on its own page");
  b.Move(board,2);
  var server=board with {Index=2,Revision=2,Stops=[board.Stops[0] with {Tasks=[board.Stops[0].Tasks[0] with {Done=true}]},board.Stops[1],board.Stops[2]]};
  var updated=a.Apply(server);
  Check(updated.Index==1&&updated.Revision==2&&updated.Stops[0].Tasks[0].Done,"shared completion arrives without moving local cursor");
  Check(b.Apply(server).Index==2,"other client keeps its different cursor");
  var reordered=server with {Revision=3,Stops=[server.Stops[1],server.Stops[2],server.Stops[0]]};
  Check(a.Apply(reordered).Index==0&&b.Apply(reordered).Index==1,"reordered route preserves each selected house");
  a.Move(reordered,3);
  Check(a.Apply(reordered with {Index=0,Revision=4}).Index==3,"review page survives remote navigation");
  Check(a.Apply(board with {Generation="g2",Index=0}).Index==0,"new route generation resets cursor");
  Check(b.Apply(board with {Stops=[board.Stops[0]],Index=0,Revision=4}).Index==0,"removed selected stop clamps safely");
  a.Clear();Check(a.Apply(board with {Index=2}).Index==2,"pairing change clears previous cursor");
 }
}
