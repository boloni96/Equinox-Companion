namespace EquinoxCompanion;
internal static class IndependentDialoguePolicy
{
    public static bool OwnedScene(uint quest,uint eventId,uint npc,uint intended,bool alreadyOwned,bool recent)=>
        quest!=0&&eventId==65536+quest&&((intended!=0&&npc==intended&&(recent||alreadyOwned))||(npc==0&&alreadyOwned));
}
