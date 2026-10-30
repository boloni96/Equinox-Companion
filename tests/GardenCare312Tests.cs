using EquinoxCompanion;
internal static class GardenCare312Tests
{
    public static void Run()
    {
        var now=DateTimeOffset.UtcNow;
        SharedGardenBed Bed(int n,string state="confirmed",bool ready=true)=>new(n,"Mirror Apple","Grade 3 Thanalan Topsoil",state,"Mirror Apple","Grade 3 Thanalan Topsoil",now.AddDays(-5),now.AddHours(-13),5,ready,NextTend:now.AddHours(-1),Order:n,PlantEvent:"plant",ObservedAt:now);
        var beds=new[]{Bed(1),Bed(2,"planned",false)};
        var definition=new GardenPlanDefinition(new("Curiel Root","cross"),[new(1,1,"Mirror Apple","Grade 3 Thanalan Topsoil"),new(2,2,"Krakka Root","Grade 3 Thanalan Topsoil",1)]);
        var plan=new SharedGardenPlan("house","House","World","Mist",1,1,1,"Curiel Root",now.AddDays(-6),beds,Definition:definition);
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        Check(GardenCarePolicy.Protection(plan,beds[0]).Contains("Bed 2"),"Pending planting protects its standing neighbour");
        Check(!GardenCarePolicy.Eligible(plan,beds[0],GardenCareAction.Harvest,now),"Bulk harvest cannot bypass protection");
        var done=plan with{Beds=[beds[0],beds[1] with{Status="confirmed"}]};
        Check(GardenCarePolicy.Protection(done,beds[0])=="","Confirmed planting releases parent even before offspring harvest");
        Check(GardenCarePolicy.Eligible(done,beds[0],GardenCareAction.Harvest,now),"Ready parent can be harvested for seeds");
        Check(!GardenCarePolicy.Eligible(done,beds[0],GardenCareAction.Tend,now),"Tending ignores mature beds");
        Check(GardenCarePolicy.Eligible(plan,beds[1],GardenCareAction.Tend,now),"Growing due bed receives tending");
        Check(GardenAutoPlant.PartialHarvest(plan),"Mixed maturity requires partial harvest confirmation");
        Check(GardenCarePolicy.Protection(done,beds[0] with{KeepMature=true}).Length>0,"Explicit keep persists after a plan finishes");
        Check(!GardenCarePolicy.Eligible(plan,beds[1] with{LastFertilized=now},GardenCareAction.Fertilize,now),"Fertilizer cooldown");
        var starter=Bed(1,"replant",false) with{ReplantOrder=9,StarterSoil="Potting Soil",ActualSoil="Potting Soil",Planted=now.AddDays(-4)};
        var replace=plan with{Beds=[starter, beds[1] with{Status="confirmed"}]};
        Check(GardenAutoPlant.Next(replace)==(1,GardenCareAction.RemoveStarter),"Verified starter gets removal step");
        Check(GardenAutoPlant.Next(replace with{Beds=[starter with{ActualCrop="Empty"},beds[1] with{Status="confirmed"}]})==(1,GardenCareAction.Plant),"Removal is followed by planting");
        try{GardenAutoPlant.Next(replace with{Beds=[starter with{ActualSoil="Grade 3 Thanalan Topsoil"},beds[1] with{Status="confirmed"}]});throw new Exception("Removed unrelated crop");}catch(InvalidOperationException){}
        Check(!GardenAutoPlant.MatchesConfirmation(GardenCareAction.RemoveStarter,"Log out?"),"Unrelated confirmation rejected");
        var empty=plan with{Beds=Enumerable.Range(1,8).Select(n=>Bed(n,"planned",false) with{ActualCrop="Empty",ActualSoil="",Planted=null}).ToArray()};
        var favourite=new GardenFavourite("saved-cross","sir","Sir","My saved cross","Curiel Root","Mirror Apple","Krakka Root",["Curiel Root"]);
        var saved=GardenPlanEditing.Build(favourite,empty);
        var applied=GardenPlanEditing.WithDefinition(empty,saved,now);
        Check(saved.Steps.Length==8&&saved.Steps[0].ReplantOrder==9&&saved.Steps[0].StarterSoil=="Potting Soil","Existing favourite preserves the temporary starter and replacement");
        Check(saved.Steps.All(b=>b.Soil=="Grade 3 Thanalan Topsoil")&&saved.Steps[1].Crop=="Krakka Root","Favourite retains exact cross soil and alternating seeds");
        Check(GardenAutoPlant.Next(applied)==(1,GardenCareAction.Plant),"Auto plant accepts an existing favourite applied to an empty batch");
        Check(GardenCarePolicy.TargetAllowed(10,0,true,GardenCareAction.Harvest),"Game-cleared target is allowed after harvest was sent");
        Check(GardenCarePolicy.TargetAllowed(10,0,true,GardenCareAction.RemoveStarter),"Starter removal can clear target");
        Check(!GardenCarePolicy.TargetAllowed(10,11,true,GardenCareAction.Harvest),"Different selected target still stops");
        Check(!GardenCarePolicy.TargetAllowed(10,0,false,GardenCareAction.Harvest),"Cleared target before sending still stops");
        foreach(var action in new[]{GardenCareAction.Tend,GardenCareAction.Fertilize}){
            Check(GardenCarePolicy.TargetAllowed(10,0,true,action),$"{action} may clear target after submission");
            Check(!GardenCarePolicy.TargetAllowed(10,0,false,action),$"{action} cannot lose target before submission");
            Check(!GardenCarePolicy.TargetAllowed(10,11,true,action),$"{action} rejects a different selected object");
            var carePort=new HarvestPort(action);var careRun=new GardenCareRun(carePort);careRun.Start([1,2,3,4,5,6,7,8],action,now);
            for(int i=0;i<32;i++)careRun.Tick(now.AddSeconds(i));
            Check(careRun.Completed==8&&!careRun.Running&&carePort.Actions==8,$"{action} continues across eight game-cleared targets without duplicate actions");
            var missingReceipt=new HarvestPort(action){Receipt=false};var waiting=new GardenCareRun(missingReceipt);waiting.Start([1,2],action,now);
            waiting.Tick(now);waiting.Tick(now.AddSeconds(1));waiting.Tick(now.AddSeconds(2));waiting.Tick(now.AddSeconds(23));
            Check(!waiting.Running&&waiting.Completed==0&&missingReceipt.Actions==1,$"{action} still requires a receipt and does not retry unconfirmed work");
        }
        var house=GardenCarePolicy.HouseHarvest([done with{Batch=2},done with{HouseId="other"},done,done with{Batch=3,Beds=[beds[0] with{KeepMature=true}]}],"house",now);
        Check(house.Select(p=>p.Batch).SequenceEqual([1,2]),"House harvest queues eligible batches in order and excludes protected beds and other houses");
        var mature=done with {Beds=[Bed(1)]};
        var growing=done with {Beds=[Bed(1,"confirmed",false) with {NextTend=now.AddMinutes(-1)}]};
        foreach(var phase in new[]{"plan","grow","waiting","review"}){
            var rule=new AdventureTaskRule(Kind:"growingcycle",HouseId:growing.HouseId,Batch:growing.Batch,Phase:phase);
            Check(GardenCarePolicy.RouteTendingNeeded(rule,growing,true,now),"Existing due crops receive route care during "+phase);
            Check(!GardenCarePolicy.RouteTendingNeeded(rule,growing,false,now),"Disabled tending stays disabled during "+phase);
            Check(!GardenCarePolicy.RouteTendingNeeded(rule,mature,true,now),"Mature crops do not create tending work during "+phase);
            Check(!GardenCarePolicy.RouteTendingNeeded(rule,growing with{HouseId:"unselected"},true,now),"Route care stays on selected house during "+phase);
            Check(!GardenCarePolicy.RouteTendingNeeded(rule,growing with{Batch=3},true,now),"Route care stays on selected batch during "+phase);
            Check(!GardenCarePolicy.RouteTendingNeeded(rule,growing with{Beds=[growing.Beds[0] with{NextTend=now.AddHours(12)}]},true,now),"Recently tended batch does not create repeat work during "+phase);
        }
        var watered=growing with {Batch=2,Beds=[growing.Beds[0] with {NextTend=now.AddHours(12),LastFertilized=now}]};
        Check(GardenCarePolicy.HouseCare([mature,growing,watered,growing with{HouseId="other"}],"house",GardenCareAction.Tend,now).Length==1,"House tending skips mature, recently tended and other houses");
        Check(GardenCarePolicy.HouseCare([mature,growing,watered],"house",GardenCareAction.Fertilize,now).Length==1,"House fertilizing respects maturity and cooldown");
        Check(GardenCarePolicy.Eligible(watered,watered.Beds[0],GardenCareAction.Tend,now,true),"Manual Tend all includes recently watered crops");
        Check(!GardenCarePolicy.Eligible(watered,watered.Beds[0],GardenCareAction.Tend,now),"Automatic Tend due still skips recently watered crops");
        Check(GardenCarePolicy.HouseCare([mature,growing,watered,growing with{HouseId="other"}],"house",GardenCareAction.Tend,now,true).Length==2,"Manual house tending includes watered batches but excludes mature and other houses");
        Check(!GardenCarePolicy.Eligible(mature,mature.Beds[0],GardenCareAction.Tend,now,true),"Manual Tend all still skips mature crops");
        Check(!GardenCarePolicy.Eligible(watered,watered.Beds[0] with{DeadConfirmedAt=now},GardenCareAction.Tend,now,true),"Manual Tend all skips dead crops");

        var pending=new GardenPendingDisplay();var builds=0;var sent=new List<string>();
        IEnumerable<SyncEvent> Read(){builds++;return [];}
        var roster=new object();for(var i=0;i<600;i++)pending.Get(growing,roster,"same",i*16,Read,()=>sent);
        Check(builds==1,"Unchanged garden display does not rescan history over 600 frames");
        pending.Get(growing,roster,"new-receipt",10000,Read,()=>sent);
        Check(builds==2,"New action or acknowledgment refreshes queued beds immediately");
        var housePlant=GardenAutoPlant.HousePlans([applied with{Batch=2},applied,applied with{HouseId="other"},applied with{Batch=3,Definition=null},applied with{Batch=3,CompletedAt=now}],"house");
        Check(housePlant.Select(p=>p.Batch).SequenceEqual([1,2]),"House planting uses unfinished saved plans in batch order only");
        Check(housePlant.All(p=>p.Definition==applied.Definition),"House queue preserves each saved definition");
        const string plantPrompt="Prepare the bed with a bag of potting soil and a bag of krakka root seeds?";
        Check(GardenAutoPlant.MatchesPlantConfirmation(plantPrompt,"Krakka Root Seeds","Potting Soil"),"Observed prepare-bed confirmation accepts exact planned seed and soil");
        Check(!GardenAutoPlant.MatchesPlantConfirmation(plantPrompt,"Mirror Apple Seeds","Potting Soil"),"Wrong seed confirmation rejected");
        Check(!GardenAutoPlant.MatchesPlantConfirmation(plantPrompt,"Krakka Root Seeds","Grade 3 Thanalan Topsoil"),"Wrong soil confirmation rejected");
        const string bedTwoPrompt="Prepare the bed with a bag of grade 3 Thanalan topsoil and a bag of mirror apple seeds?";
        Check(GardenAutoPlant.MatchesPlantConfirmation(bedTwoPrompt,"Mirror Apple Seeds","Grade 3 Thanalan Topsoil"),"Reported second-bed prompt matches exact materials");
        Check(!GardenAutoPlant.MatchesPlantConfirmation(bedTwoPrompt,"Mirror Apple Seeds","Grade 2 Thanalan Topsoil"),"Different topsoil grade rejected");
        Check(!GardenAutoPlant.MatchesPlantConfirmation(bedTwoPrompt,"Old World Fig Seeds","Grade 3 Thanalan Topsoil"),"Different cross seed rejected");
        Check(!GardenAutoPlant.MatchesPlantConfirmation("Discard Krakka Root Seeds and Potting Soil?","Krakka Root Seeds","Potting Soil"),"Unrelated item confirmation rejected");
        Check(GardenCarePolicy.TargetAllowed(10,0,true,GardenCareAction.Plant),"Game-cleared target allowed after planting confirmation was sent");
        Check(!GardenCarePolicy.TargetAllowed(10,0,false,GardenCareAction.Plant),"Plant target clearing before confirmation still stops");
        Check(!GardenCarePolicy.TargetAllowed(10,11,true,GardenCareAction.Plant),"Different object after planting still stops");
        var fresh=Bed(1) with{Planted=now,Watered=now,Ready=false,NextTend=now.AddHours(12)};
        var freshPlan=done with{At=now.AddMinutes(-1),Beds=[fresh]};
        Check(GardenAutoPlant.NeedsPostPlantTend(freshPlan,fresh),"Auto plant tends fresh final crops even before the ordinary due time");
        Check(!GardenAutoPlant.NeedsPostPlantTend(freshPlan,fresh with{Watered=now.AddSeconds(1)}),"Resume skips an already tended final crop");
        Check(!GardenAutoPlant.NeedsPostPlantTend(freshPlan,fresh with{Status="starter"}),"Temporary starter is not tended before replacement");
        Check(!GardenAutoPlant.NeedsPostPlantTend(freshPlan,fresh with{Planted=now.AddDays(-1)}),"Post-plant tending excludes crops from an older plan");
        Check(!GardenAutoPlant.NeedsPostPlantTend(freshPlan,fresh with{Ready=true}),"Post-plant tending skips mature crops");
        var matureBatch=done with{Beds=[Bed(1),Bed(2)],CompletedAt=now.AddDays(-1)};
        var prep=GardenAutoPlant.PrepareRouteBeds(matureBatch,definition,now,true,true);
        Check(prep.Action==GardenCareAction.Harvest&&prep.Beds.SequenceEqual([1,2])&&!prep.Empty,"Ready outgoing batch is harvested before applying incoming layout");
        Check(GardenAutoPlant.PrepareRouteBeds(matureBatch,definition,now,false,false).Action==null,"Harvest option remains required");
        var mixed=matureBatch with{Beds=[Bed(1),Bed(2) with{Ready=false,HarvestAt=now.AddDays(1)}]};
        Check(GardenAutoPlant.PrepareRouteBeds(mixed,definition,now,true,true).Action==GardenCareAction.Tend,"Mixed maturity tends growing crops instead of harvesting half a batch");
        var growingWait328=mixed with{Beds=mixed.Beds.Select(b=>b with{NextTend=now.AddHours(12),HarvestAt=now.AddDays(1)}).ToArray()};
        Check(!GardenAutoPlant.PrepareRouteBeds(growingWait328,definition,now,true,true).Empty&&GardenAutoPlant.PrepareRouteBeds(growingWait328,definition,now,true,true).Action==null,"Growing watered batch has no work and must not trigger travel");
        Check(GardenAutoPlant.PrepareRouteBeds(growingWait328,definition,now.AddHours(13),true,true).Action==GardenCareAction.Tend,"Growing batch becomes tendable next day without waiting for maturity");
        var estimated=growingWait328 with{Beds=growingWait328.Beds.Select(b=>b with{HarvestAt=now.AddMinutes(-1)}).ToArray()};
        Check(GardenAutoPlant.PrepareRouteBeds(estimated,definition,now,true,true).Action==GardenCareAction.Inspect,"Estimated maturity can request a real inspection");
        Check(GardenAutoPlant.PrepareRouteBeds(estimated,definition,now,true,true,false).Action==null,"Recent not-ready inspection does not trigger another trip");
        Check(GardenAutoPlant.PrepareRouteBeds(estimated,definition,now.AddHours(13),true,true,false).Action==GardenCareAction.Tend,"Inspection cooldown cannot suppress due tending");

        Check(GardenAutoPlant.PrepareRouteBeds(matureBatch with{Beds=[Bed(1) with{KeepMature=true},Bed(2)]},definition,now,true,false).Action==null,"Explicit protected neighbour blocks preparation harvest");
        Check(GardenAutoPlant.PrepareRouteBeds(plan,definition,now,true,false).Action==null,"Unfinished outgoing cross is preserved");
        var cleared=matureBatch with{Beds=matureBatch.Beds.Select(b=>b with{ActualCrop="Empty",Planted=null}).ToArray()};
        Check(GardenAutoPlant.PrepareRouteBeds(cleared,definition,now,true,true).Empty,"Confirmed empty beds release next planting stage");
        Check(!GardenAutoPlant.PrepareRouteBeds(matureBatch with{Beds=[cleared.Beds[0],Bed(2)]},definition,now,true,true).Empty,"One harvest receipt cannot release planting before remaining crops are cleared");
        Check(GardenAutoPlant.PrepareRouteBeds(cleared with{Beds=[cleared.Beds[0] with{ObservedAt=null},cleared.Beds[1]]},definition,now,true,true).Action==GardenCareAction.Inspect,"Unobserved empty beds need inspection");
        var single=definition with{Steps=[definition.Steps[0]]};
        Check(GardenAutoPlant.PrepareRouteBeds(matureBatch,single,now,true,true).Beds.SequenceEqual([1]),"Preparation never harvests beds outside incoming layout");
        var approach=GardenPlotApproachPolicy.Path(new(0,0,0),new(0,0,0),new(0,0,10));
        Check(approach.SequenceEqual(new[]{new System.Numerics.Vector3(0,0,3)}),"Entrance step goes three metres toward door, not all the way to it");
        Check(GardenPlotApproachPolicy.Path(new(0,0,0),new(0,0,0),new(-10,0,0))[0].X==-3,"Door direction replaces stale character facing");
        try{GardenPlotApproachPolicy.Path(new(0,0,-16),new(0,0,0),new(0,0,10));throw new Exception("Distant entrance accepted");}catch(InvalidOperationException){}
        Check(GardenPlotApproachPolicy.Path(new(0,0,0),new(0,0,0),null,MathF.PI/2)[0].X>2.99f,"Missing or ambiguous door uses settled arrival facing");
        var arrival=new GardenArrivalGate();
        Check(!arrival.Ready(true,new(0,0,0),now),"Pending native teleport blocks movement even when Lifestream idle");
        Check(!arrival.Ready(false,new(0,0,0),now.AddSeconds(1)),"Idle gap does not immediately start walking");
        Check(!arrival.Ready(true,new(0,0,0),now.AddSeconds(2)),"Casting or loading resets settled arrival");
        Check(!arrival.Ready(false,new(50,0,0),now.AddSeconds(8)),"New arrival position starts settling");
        Check(!arrival.Ready(false,new(51,0,0),now.AddSeconds(10)),"Final Lifestream movement resets settling");
        Check(!arrival.Ready(false,new(51,0,0),now.AddSeconds(12)),"Arrival waits full three seconds");
        Check(arrival.Ready(false,new(51,0,0),now.AddSeconds(13)),"Stationary arrival releases approach");
        var progress=new GardenWalkProgress();progress.Reset(new(0,0,0),now);
        Check(!progress.Blocked(new(2,0,0),now.AddSeconds(2)),"Walking advances progress checkpoint");
        Check(!progress.Blocked(new(2,0,0),now.AddSeconds(4)),"Brief pause tolerated");
        Check(progress.Blocked(new(2,0,0),now.AddSeconds(5)),"Wall after initial movement detected in three seconds");
        progress.Reset(new(2,0,0),now.AddSeconds(6));
        Check(!progress.Blocked(new(3,0,0),now.AddSeconds(8)),"New walk resets stalled state");
        var recovery=new GardenWalkRecovery();recovery.Reset(new(0,0,0),new(0,0,3));
        var left=recovery.Next(new(0,0,1),now)!;
        Check(left.Length==2&&left[0]==new System.Numerics.Vector3(-.8f,0,1)&&left[1]==new System.Numerics.Vector3(0,0,3),"First recovery steps left and retains destination");
        var right=recovery.Next(left[0],now.AddSeconds(4))!;
        Check(Math.Abs(right[0].X-.8f)<.001f&&right[1]==left[1],"Second recovery tries other side without changing destination");
        var leftAgain=recovery.Next(right[0],now.AddSeconds(8))!;var rightAgain=recovery.Next(leftAgain[0],now.AddSeconds(12))!;
        Check(leftAgain[0].X<0&&rightAgain[0].X>0,"Recovery alternates left right left right");
        Check(recovery.Next(rightAgain[0],now.AddSeconds(20))!=null,"Recovery continues beyond four attempts within time budget");
        Check(recovery.Deadline==now.AddSeconds(30),"Recovery retries never extend initial thirty second deadline");
        Check(recovery.Next(rightAgain[0],now.AddSeconds(30))==null,"Recovery stops at thirty seconds");
        recovery.Reset(new(0,0,0),new(0,0,3));
        Check(recovery.Next(new(float.NaN,0,0),now)==null,"Invalid recovery position rejected");
        var blockedPort=new Port{OpenReady=false,MovementRecoveryUntil=now.AddSeconds(33)};
        var blockedRun=new GardenCareRun(blockedPort);blockedRun.Start([1],GardenCareAction.Plant,now);
        blockedRun.Tick(now.AddSeconds(21));Check(blockedRun.Running&&blockedPort.Acts==0,"Walking recovery can outlast ordinary twenty second opening deadline without planting");
        blockedRun.Tick(now.AddSeconds(33));Check(!blockedRun.Running&&blockedPort.Acts==0,"Walking recovery deadline stops runner before sending an action");
        var planting=new HarvestPort(GardenCareAction.Plant);var plantingRun=new GardenCareRun(planting);plantingRun.Start([1,2,3],GardenCareAction.Plant,now);
        for(int i=0;i<12;i++)plantingRun.Tick(now.AddSeconds(i));
        Check(plantingRun.Completed==3&&!plantingRun.Running&&planting.Actions==3,"Three planting receipts survive game-cleared targets and send each action once");
        var harvesting=new HarvestPort();var harvestingRun=new GardenCareRun(harvesting);harvestingRun.Start([1,2,3],GardenCareAction.Harvest,now);
        for(int i=0;i<12;i++)harvestingRun.Tick(now.AddSeconds(i));
        Check(harvestingRun.Completed==3&&!harvestingRun.Running&&harvesting.Actions==3,"Target clears after each harvest without stopping or duplicating the next bed");
        var port=new Port();var run=new GardenCareRun(port);run.Start([1,2],GardenCareAction.Tend,now);
        run.Tick(now);run.Tick(now.AddSeconds(1));run.Tick(now.AddSeconds(2));
        Check(run.Completed==0&&port.Acts==1,"Sending does not imply success");
        run.Tick(now.AddSeconds(23));Check(!run.Running&&port.Acts==1,"No duplicate click after unconfirmed timeout");
        port.Receipt=true;run.Start([2],GardenCareAction.Tend,now.AddMinutes(1));for(int i=0;i<4;i++)run.Tick(now.AddMinutes(1).AddSeconds(i));
        Check(run.Completed==1&&!run.Running,"Restart has no stale queue");
        run.Start([1,2],GardenCareAction.Tend,now.AddMinutes(2));run.Stop("Stopped");var calls=port.Acts;run.Tick(now.AddMinutes(2));Check(port.Acts==calls,"Stop sends no further actions");
    }
    private sealed class HarvestPort(GardenCareAction expectedAction=GardenCareAction.Harvest):IGardenCarePort
    {
        private ulong expected,current;private bool sent;public int Actions;public bool Receipt=true;
        public void Validate(){if(!GardenCarePolicy.TargetAllowed(expected,current,sent,expectedAction))throw new InvalidOperationException("Selected garden target changed.");}
        public bool Open(int bed){expected=current=(ulong)bed;sent=false;return true;}
        public bool Act(int bed,GardenCareAction action){Actions++;sent=true;current=0;return true;}
        public bool Confirmed(int bed,GardenCareAction action,DateTimeOffset at)=>sent&&Receipt;
        public bool Close(int bed){expected=current=0;sent=false;return true;}public void Release(){}
    }
    private sealed class Port:IGardenCarePort
    {
        public int Acts;public bool Receipt;public bool OpenReady=true;public DateTimeOffset? MovementRecoveryUntil{get;set;}
        public void Validate(){} public bool Open(int bed)=>OpenReady;
        public bool Act(int bed,GardenCareAction action){Acts++;return true;}
        public bool Confirmed(int bed,GardenCareAction action,DateTimeOffset sent)=>Receipt;
        public bool Close(int bed)=>true;public void Release(){}
    }
}
