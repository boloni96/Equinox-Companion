namespace EquinoxCompanion;
internal static class GearDresserSelection
{
    internal static bool Matches(uint container,int slot,uint item,uint rowContainer,int rowSlot,uint rowItem)=>
        item>0&&container==rowContainer&&slot==rowSlot&&item%1000000==rowItem%1000000;
}
