namespace EquinoxCompanion;
public static class AethernetRecoveryPolicy
{
    public static bool ReturnToMain(bool alreadyTeleported,int stage,string failure)=>
        !alreadyTeleported&&stage==0&&failure=="No connected walkable route found.";
}
