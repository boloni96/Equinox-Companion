using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool followStopPending;
    private DateTimeOffset followStopNext;
    private ulong followStopCharacter;
    private void RequestFollowMovementStop(){followStopPending=true;followStopCharacter=Player.IsLoaded?Player.ContentId:followLogin;followStopNext=default;UpdateFollowMovementStop(DateTimeOffset.UtcNow);}
    private unsafe void UpdateFollowMovementStop(DateTimeOffset now)
    {
        if(followStopPending&&Player.IsLoaded&&Player.ContentId!=followStopCharacter){followStopPending=false;return;}
        if(!followStopPending||!CanIssueFollowMovement()||now<followStopNext)return;
        // Check the game's state rather than trusting our requested phase. Never
        // toggle autorun when it is already off (that would start movement).
        if(!InputManager.IsAutoRunning()){followStopPending=false;return;}
        FollowCommand("/automove");
        followStopNext=now.AddMilliseconds(500);
    }
}
