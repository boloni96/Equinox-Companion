using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EquinoxCompanion;
internal static class NativeDc443Tests
{
    [ModuleInitializer] internal static unsafe void Run()
    {
        var names=Enumerable.Repeat("",16).ToArray();names[0]="Chaos";names[1]="Light";names[4]="Materia";
        var c=NativeDcListSelection.Find(23,"Materia",names)??throw new Exception("Oceanian DC missing");
        if(c.EventParameter!=1||c.Category!=1||c.Item!=0||c.PackedItem!=256)throw new Exception("DC clicks need region-relative keys, not visible-row indices.");
        var worlds=new[]{"Bismarck","Ravana","Sephirot","Sophia","Zurvan","","",""};
        var w=NativeDcListSelection.Find(24,"Sophia",worlds)??throw new Exception("World missing");
        if(w.EventParameter!=2||w.PackedItem!=3)throw new Exception("World selections must use their own list and zero-based item.");
        if(NativeDcListSelection.Find(24,"Raiden",worlds)!=null||NativeDcListSelection.Find(23,"",names)!=null)throw new Exception("Unavailable or empty destinations must not select another row.");
        names[8]="Materia";if(NativeDcListSelection.Find(23,"Materia",names)!=null)throw new Exception("Ambiguous destination must not be selected.");
        var key=new NativeDcTreeKey{Selected=1,Item=c.PackedItem};var ptr=&key;var input=new NativeDcTreeInput{Key=&ptr,Highlight=4};
        if(sizeof(NativeDcTreeInput)!=64||sizeof(NativeDcTreeKey)!=64||Marshal.OffsetOf<NativeDcTreeInput>(nameof(NativeDcTreeInput.Key)).ToInt32()!=8||Marshal.OffsetOf<NativeDcTreeInput>(nameof(NativeDcTreeInput.Highlight)).ToInt32()!=16||(*input.Key)->Item!=256||(*input.Key)->Selected!=1)throw new Exception("Native tree selection payload ABI differs from the menu's input contract.");
        Console.WriteLine("DC region keys, world selection, ambiguity and native click payload passed.");
    }
}
