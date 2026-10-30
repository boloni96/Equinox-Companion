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
        var port=new Port();var run=new GardenCareRun(port);run.Start([1,2],GardenCareAction.Tend,now);
        run.Tick(now);run.Tick(now.AddSeconds(1));run.Tick(now.AddSeconds(2));
        Check(run.Completed==0&&port.Acts==1,"Sending does not imply success");
        run.Tick(now.AddSeconds(23));Check(!run.Running&&port.Acts==1,"No duplicate click after unconfirmed timeout");
        port.Receipt=true;run.Start([2],GardenCareAction.Tend,now.AddMinutes(1));for(int i=0;i<4;i++)run.Tick(now.AddMinutes(1).AddSeconds(i));
        Check(run.Completed==1&&!run.Running,"Restart has no stale queue");
        run.Start([1,2],GardenCareAction.Tend,now.AddMinutes(2));run.Stop("Stopped");var calls=port.Acts;run.Tick(now.AddMinutes(2));Check(port.Acts==calls,"Stop sends no further actions");
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
