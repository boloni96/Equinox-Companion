namespace EquinoxCompanion;
// Gear already in the Armoury Chest is not part of "subtract items in bags".
// Count only its net change during this run, so automatic gear placement can
// acknowledge a withdrawal without treating previously owned gear as received.
internal sealed class WithdrawalArmouryReceipt
{
    private Dictionary<uint,int> initial=[];
    internal void Begin(IReadOnlyDictionary<uint,int> armoury)=>initial=armoury.ToDictionary(x=>x.Key,x=>x.Value);
    internal bool Tracks(uint item)=>initial.ContainsKey(item);
    internal int Held(uint item,int bags,int armoury)=>Math.Max(0,checked(bags+(Tracks(item)?armoury-initial[item]:0)));
}
