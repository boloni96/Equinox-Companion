using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Storage497Tests
{
 [ModuleInitializer] internal static void Run(){
  foreach(var equipped in new[]{false,true})foreach(byte dye1 in new byte[]{0,93})foreach(byte dye2 in new byte[]{0,118})foreach(var skipDyed in new[]{false,true})foreach(var materia in new[]{false,true})foreach(var condition in new[]{0,29999,30000})foreach(var quantity in new[]{0,1,2}){
   var expected=quantity==1&&!equipped&&(!skipDyed||dye1==0&&dye2==0)&&!materia&&condition>=30000;
   var reason=GearStoragePolicy.CopyRejection(quantity,equipped,dye1,dye2,skipDyed,materia,condition);
   if(GearStoragePolicy.SafeCopy(quantity,equipped,dye1,dye2,skipDyed,materia,condition)!=expected||(reason.Length==0)!=expected)throw new Exception("Storage rejection reason changed eligibility");
  }
  if(!GearStoragePolicy.CopyRejection(1,false,0,118,true,false,30000).Contains("Dyed"))throw new Exception("Second dye channel must be explained");
  if(!GearStoragePolicy.CopyRejection(1,false,0,0,true,true,30000).Contains("Materia"))throw new Exception("Materia rejection must be explained");
  if(!GearStoragePolicy.CopyRejection(1,false,0,0,true,false,29999).Contains("repair"))throw new Exception("Repair rejection must be explained");
  Console.WriteLine("497 storage skip reasons preserve all copy protections.");
 }
}
