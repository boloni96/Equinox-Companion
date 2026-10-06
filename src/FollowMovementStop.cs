using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool followStopPending;
    private DateTimeOffset followStopNext;
    private ulong followStopCharacter;
    private bool followStopIssued;
    private void RequestFollowMovementStop(){if(followStopPending)return;followStopIssued=false;followStopPending=true;followStopCharacter=Player.IsLoaded?Player.ContentId:followLogin;followStopNext=default;UpdateFollowMovementStop(DateTimeOffset.UtcNow);}
    private unsafe void UpdateFollowMovementStop(DateTimeOffset now)
    {
        if(followStopPending&&Player.IsLoaded&&Player.ContentId!=followStopCharacter){followStopPending=false;return;}
        if(!followStopPending||now<followStopNext)return;
        if(followStopIssued&&!InputManager.IsAutoRunning()){followStopPending=false;return;}
        if(!CanIssueFollowMovement())return;
        // Check the game's state rather than trusting our requested phase. Never
        // toggle autorun when it is already off (that would start movement).
        FollowCommand("/automove off");
        followStopIssued=true;
        followStopNext=now.AddMilliseconds(500);
    }
}
