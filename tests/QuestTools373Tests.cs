using EquinoxCompanion;
internal static class QuestTools373Tests
{
    internal static void Run(){
        void Check(string name,bool good){if(!good)throw new Exception(name);}
        Check("current character needs no login slot",QuestToolsPolicy.EventCharacterReady("current","current",-1));
        Check("switch waits without login slot",!QuestToolsPolicy.EventCharacterReady("current","next",-1));
        Check("known slot permits switch",QuestToolsPolicy.EventCharacterReady("current","next",0));
        Check("missing identity blocks run",!QuestToolsPolicy.EventCharacterReady("","",0));
        Check("invalid slot blocks switch",!QuestToolsPolicy.EventCharacterReady("current","next",16));
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
        Check("no celebration for empty account",!AccountCelebrationPolicy.Complete(0,0));
        Check("no celebration for partial account",!AccountCelebrationPolicy.Complete(3,2));
        Check("no celebration after skipped character",!AccountCelebrationPolicy.Complete(3,3,1));
        Check("all account goals required",AccountCelebrationPolicy.Complete(3,3));
        Check("no celebration on initial sync",!AccountCelebrationPolicy.Transition(null,true));
        Check("completion transition celebrates",AccountCelebrationPolicy.Transition(false,true));
        Check("repeated completion stays quiet",!AccountCelebrationPolicy.Transition(true,true));
        Check("five second fade",AccountCelebrationPolicy.Alpha(0)==0&&AccountCelebrationPolicy.Alpha(.25)==.5f&&AccountCelebrationPolicy.Alpha(1)==1&&AccountCelebrationPolicy.Alpha(3)==1&&AccountCelebrationPolicy.Alpha(4.625)==.5f&&AccountCelebrationPolicy.Alpha(5)==0);
        Check("accept may use entrance shard",NativeEventPolicy.EntranceShortcut(NativeEventStep.Accept,false));
        Check("square step never uses entrance shard",!NativeEventPolicy.EntranceShortcut(NativeEventStep.EnterSquare,false));
        Check("walking past crystal does not teleport",!NativeEventPolicy.EntranceShortcut(NativeEventStep.Accept,true));
        Check("missing quest accepts",NativeEventPolicy.Step(false,false,0)==NativeEventStep.Accept);
        Check("accepted quest waits for sequence",NativeEventPolicy.Step(false,true,0)==NativeEventStep.Wait);
        Check("accepted quest enters square",NativeEventPolicy.Step(false,true,1)==NativeEventStep.EnterSquare);
        Check("final sequence completes",NativeEventPolicy.Step(false,true,255)==NativeEventStep.Complete);
        Check("unknown sequence never guesses",NativeEventPolicy.Step(false,true,2)==NativeEventStep.Unsupported);
        Check("native completion wins",NativeEventPolicy.Step(true,true,255)==NativeEventStep.Finished);
        Check("wrong NPC scene rejected",!NativeEventPolicy.Scene(71002,1058754,1058752));
        Check("unrelated event rejected",!NativeEventPolicy.Scene(71003,1058752,1058752));
        Check("expected quest scene accepted",NativeEventPolicy.Scene(71002,1058752,1058752));
        Check("owned square confirmation accepted",NativeEventPolicy.EntranceConfirmation(NativeEventStep.EnterSquare,1058754,144,true,"Keybound Square","Proceed to Keybound Square?"));
        Check("unowned square confirmation rejected",!NativeEventPolicy.EntranceConfirmation(NativeEventStep.EnterSquare,1058754,144,false,"Keybound Square","Proceed to Keybound Square?"));
        Check("other destination rejected",!NativeEventPolicy.EntranceConfirmation(NativeEventStep.EnterSquare,1058754,144,true,"Keybound Square","Proceed to another place?"));
        Check("keyboard warning format codes tolerated",NativeEventPolicy.KeyboardConfirmation("","A keyboard is required to participate in \u0002\u0010\u0001\u0003Keybound Brawler. The Gamepad and On-screen Keyboard cannot be used, so please ensure that you have a keyboard connected before registering for a game."));
        Check("other warning not accepted",!NativeEventPolicy.KeyboardConfirmation("","Delete this item?"));
        Check("empty prompt rejected",!NativeEventPolicy.Prompt("",""));
        Check("different confirmation rejected",!NativeEventPolicy.Prompt("Continue?","Buy this item?"));
        Check("localized line wrapping tolerated",NativeEventPolicy.Prompt("Continue now?","Continue\nnow?"));
        Check("distant unverified walk rejected",!NativeEventPolicy.NearbyLeg(new(0,0,0),new(100,0,0)));
        Check("different floor rejected",!NativeEventPolicy.NearbyLeg(new(0,0,0),new(0,20,0)));
        Check("invalid coordinate rejected",!NativeEventPolicy.NearbyLeg(new(0,0,0),new(float.NaN,0,0)));
        var watch=new NativeEventWalkWatch();var start=DateTimeOffset.UtcNow;watch.Start(30,start);
        Check("stall reported",watch.Check(30,start.AddSeconds(9)).Length>0);
        watch.Start(30,start);Check("forward progress continues",watch.Check(25,start.AddSeconds(6)).Length==0);
        Check("sideways wandering times out",watch.Check(26,start.AddSeconds(15)).Length>0);
        var history=new KeyboundRunHistory();
        KeyboundRunEvent Event(string run,string kind)=>new(DateTimeOffset.UtcNow,"session",run,"user-button",kind,42,"test",null,null,null,null,null,null,[],[]);
        history.Add(Event("first","started"));history.Add(Event("first","stopped"));history.Add(Event("second","started"));
        Check("restart retains prior stop",history.Snapshot().Select(e=>e.Run+":"+e.Kind).SequenceEqual(new[]{"first:started","first:stopped","second:started"}));
        var restored=new KeyboundRunHistory();restored.Restore(System.Text.Json.JsonSerializer.Deserialize<KeyboundRunEvent[]>(System.Text.Json.JsonSerializer.Serialize(history.Snapshot()))!);
        Check("reload preserves run identities",restored.Snapshot().Select(e=>e.Run).SequenceEqual(new[]{"first","first","second"}));
        for(var i=0;i<600;i++)history.Add(Event(i.ToString(),"state"));
        Check("history bounded without losing latest",history.Snapshot().Length==KeyboundRunHistory.Limit&&history.Snapshot()[^1].Run=="599");
        Console.WriteLine("Quest tools and account celebration tests passed.");
    }
}


