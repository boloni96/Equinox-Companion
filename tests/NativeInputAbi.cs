using System.Runtime.InteropServices;
using EquinoxCompanion;
internal static unsafe class NativeInputAbi
{
    // Simulate a native byte result with nonzero undefined upper return bits.
    [UnmanagedCallersOnly]
    private static uint Input(nint input,int id)=>0x123400u | (id==322?1u:0u);
    private delegate bool LegacyRead(nint input,int id);
    public static (bool LegacyFalse, byte FixedFalse, byte FixedTrue) Probe()
    {
        var address=(nint)(delegate* unmanaged<nint,int,uint>)&Input;
        var legacy=Marshal.GetDelegateForFunctionPointer<LegacyRead>(address);
        var current=Marshal.GetDelegateForFunctionPointer<FollowInputRead>(address);
        return (legacy(0,1),current(0,1),current(0,322));
    }
}
