using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset followActorLastLoaded;
    private bool KeepFollowSessionDuringLoading(DateTimeOffset now)
    {
        if(Player.IsLoaded&&Player.ContentId!=0){followActorLastLoaded=now;return true;}
        return !Conditions[ConditionFlag.LoggingOut]&&followLogin!=0&&
            (Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||lifestreamTravelOwned||
             FollowLoadingPolicy.Grace(now,followActorLastLoaded));
    }
}
