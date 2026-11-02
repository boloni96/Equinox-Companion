using EquinoxCompanion;
internal static class QuestTools373Tests
{
    internal static void Run(){
        void Check(string name,bool good){if(!good)throw new Exception(name);}
        Check("full sheet ID maps to quest",QuestToolsPolicy.ShortId(71002)==5466);
        Check("completed quest still farms missing rewards",QuestToolsPolicy.Next(true,true,6380,2000,0,0)==EventRoutineAction.Farm);
        Check("enough funds buys and uses",QuestToolsPolicy.Next(true,true,6380,6459,0,0)==EventRoutineAction.BuyAndUse);
        Check("obtained but unused rewards still used",QuestToolsPolicy.Next(true,true,0,200,17,0)==EventRoutineAction.BuyAndUse);
        Check("only fully finished character advances",QuestToolsPolicy.Next(true,true,0,200,0,0)==EventRoutineAction.Next);
        Check("unknown wishlist never means done",QuestToolsPolicy.Next(true,false,0,200,0,0)==EventRoutineAction.Verify);
        Check("uncertain purchases never advance",QuestToolsPolicy.Next(true,true,0,200,0,1)==EventRoutineAction.Verify);
        Check("quest needed regardless rewards",QuestToolsPolicy.Next(false,true,0,0,0,0)==EventRoutineAction.Quest);
        foreach(var name in new[]{"ChatLog","ChatLogPanel_0","InputString","LetterList","ConfigSystem","_TitleMenu"})
            Check("private input excluded: "+name,!QuestToolsPolicy.RecordWindow(name));
        foreach(var name in new[]{"KTGEnterDialogue","KTGResult","JournalAccept","JournalResult","Talk","SelectString","UnknownEventWindow"})
            Check("event windows captured: "+name,QuestToolsPolicy.RecordWindow(name));
        Check("duty route deferred",QuestRoutePolicy.Review("{\"QuestSequence\":[{\"Steps\":[{\"InteractionType\":\"Duty\"}]}]}").Duty);
        Check("solo duty deferred",QuestRoutePolicy.Review("{\"QuestSequence\":[{\"Steps\":[{\"InteractionType\":\"SinglePlayerDuty\"}]}]}").Duty);
        Check("unverified route blocked",!QuestRoutePolicy.Review("{}").Readable);
        var route=QuestRoutePolicy.Review("{\"QuestSequence\":[{\"Steps\":[{\"InteractionType\":\"CompleteQuest\",\"NextQuestId\":5467}]}]}");
        Check("explicit quest line successor",route.Readable&&!route.Duty&&route.Next.SequenceEqual(new uint[]{5467}));
        Console.WriteLine("Quest tools 373 tests passed.");
    }
}
