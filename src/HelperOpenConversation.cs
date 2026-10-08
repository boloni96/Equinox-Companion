using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private HelperNpc? helperLocalNpc;
    private string helperLocalKind="";
    private DateTimeOffset helperLocalClickedAt;
    private unsafe void ObserveHelperLocalInteraction(IGameObject clicked)
    {
        // Capture provenance before the leader-only sharing gate. This never sends an action.
        helperLocalNpc=null;
        if(!helperPermission.Active||!helperPermission.Quest||HelperPaused||helperPermission.QuestPaused||!Player.IsLoaded||Objects.LocalPlayer is not {} self)return;
        if(Vector3.Distance(self.Position,clicked.Position)>clicked.HitboxRadius+4)return;
        var map=AgentMap.Instance();if(map==null)return;
        helperLocalNpc=new("",clicked.BaseId,clicked.Name.TextValue,Client.TerritoryType,map->CurrentMapId,Player.CurrentWorld.RowId,FollowTravelPosition.From(clicked.Position),FollowTravelPosition.From(self.Position),self.Rotation);
        helperLocalKind=clicked.ObjectKind.ToString();helperLocalClickedAt=DateTimeOffset.UtcNow;
        ResetHelperNativeScene();
        RecordFollowTravel("Helper local NPC interaction observed",new {npc=clicked.Name.TextValue,clicked.BaseId});
    }
    private unsafe bool TryAdoptHelperConversation(HelperAction interaction,DateTimeOffset now)
    {
        var map=AgentMap.Instance();
        if(map==null||map->CurrentMapId!=interaction.Npc.Map)return false;
        var talk=HelperTalk();
        if(!HelperOpenConversationPolicy.Matches(helperIncoming.ToArray(),helperLocalNpc,helperLocalKind,
            helperLocalClickedAt,now,followArmedAt,HelperScene(),talk.Text,talk.Signature))return false;
        helperNpcActive=interaction.Npc;helperLocalNpc=null;
        RecordFollowTravel("Helper matching open conversation resumed",new {npc=interaction.Npc.Name,interaction.QuestId,interaction.Scene});
        CompleteHelperAction(now);return true;
    }
}
