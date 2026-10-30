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
        Check(!GardenCarePolicy.TargetAllowed(10,0,true,GardenCareAction.Tend),"Tending does not exempt target changes");
        var house=GardenCarePolicy.HouseHarvest([done with{Batch=2},done with{HouseId="other"},done,done with{Batch=3,Beds=[beds[0] with{KeepMature=true}]}],"house",now);
        Check(house.Select(p=>p.Batch).SequenceEqual([1,2]),"House harvest queues eligible batches in order and excludes protected beds and other houses");
        var mature=done with {Beds=[Bed(1)]};
        var growing=done with {Beds=[Bed(1,"confirmed",false) with {NextTend=now.AddMinutes(-1)}]};
        var watered=growing with {Batch=2,Beds=[growing.Beds[0] with {NextTend=now.AddHours(12),LastFertilized=now}]};
        Check(GardenCarePolicy.HouseCare([mature,growing,watered,growing with{HouseId="other"}],"house",GardenCareAction.Tend,now).Length==1,"House tending skips mature, recently tended and other houses");
        Check(GardenCarePolicy.HouseCare([mature,growing,watered],"house",GardenCareAction.Fertilize,now).Length==1,"House fertilizing respects maturity and cooldown");
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
        private ulong expected,current;private bool sent;public int Actions;
        public void Validate(){if(!GardenCarePolicy.TargetAllowed(expected,current,sent,expectedAction))throw new InvalidOperationException("Selected garden target changed.");}
        public bool Open(int bed){expected=current=(ulong)bed;sent=false;return true;}
        public bool Act(int bed,GardenCareAction action){Actions++;sent=true;current=0;return true;}
        public bool Confirmed(int bed,GardenCareAction action,DateTimeOffset at)=>sent;
        public bool Close(int bed){expected=current=0;sent=false;return true;}public void Release(){}
    }
    private sealed class Port:IGardenCarePort
    {
        public int Acts;public bool Receipt;
        public void Validate(){} public bool Open(int bed)=>true;
        public bool Act(int bed,GardenCareAction action){Acts++;return true;}
        public bool Confirmed(int bed,GardenCareAction action,DateTimeOffset sent)=>Receipt;
        public bool Close(int bed)=>true;public void Release(){}
    }
}
