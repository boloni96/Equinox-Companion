namespace EquinoxCompanion;
public static class HelperOpenConversationPolicy
{
    public static bool Matches(HelperAction[] steps,HelperNpc? local,string kind,DateTimeOffset clicked,
        DateTimeOffset now,long sessionStarted,string scene,string text,string signature)
    {
        if(local==null||clicked.ToUnixTimeMilliseconds()<sessionStarted||now<clicked||now-clicked>TimeSpan.FromSeconds(60)||steps.Length<3)return false;
        var interaction=steps[0];var confirmation=steps[1];var talk=steps[2];var npc=interaction.Npc;
        return interaction.Kind=="interact"&&HelperQuestScenePolicy.Quest(interaction.QuestId)&&
            kind==HelperQuestScenePolicy.ObjectKind(interaction.Text)&&
            local.BaseId==npc.BaseId&&local.Name==npc.Name&&local.World==npc.World&&local.Territory==npc.Territory&&local.Map==npc.Map&&
            System.Numerics.Vector3.DistanceSquared(local.Position.Point,npc.Position.Point)<1&&
            HelperQuestScenePolicy.Confirmation(confirmation)&&confirmation.Npc==npc&&confirmation.QuestId==interaction.QuestId&&
            interaction.Scene==scene&&confirmation.Scene==scene&&
            talk.Kind=="talk"&&talk.Npc==npc&&talk.QuestId==interaction.QuestId&&talk.Scene==scene&&
            text.Length>0&&signature.Length>0&&talk.Text==text&&talk.Signature==signature;
    }
}
