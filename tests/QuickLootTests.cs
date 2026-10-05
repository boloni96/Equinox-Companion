using EquinoxCompanion;
static class QuickLootTests
{
    public static void Run(Action<string,string?,string?> check)
    {
        var s=new QuickLootSettings();
        var f=new QuickLootFacts(100,20,true,true,true,false,false,false,false,"",true,500,false,true,600,550,1500);
        void Expect(string name,QuickLootRoll expected,QuickLootFacts? facts=null,QuickLootRoll intent=QuickLootRoll.Need)=>check("QuickLoot "+name,QuickLootPolicy.Decide(s,facts??f,intent).Roll.ToString(),expected.ToString());
        check("QuickLoot auto off by default",s.Automatic.ToString(),"False");
        check("QuickLoot failed-roll pass blocked by default",s.PreventFailurePass.ToString(),"True");
        Expect("default need",QuickLootRoll.Need);
        Expect("need falls back greed",QuickLootRoll.Greed,f with {CanNeed=false});
        Expect("need falls back pass",QuickLootRoll.Pass,f with {CanNeed=false,CanGreed=false});
        Expect("greed falls back pass",QuickLootRoll.Pass,f with {CanGreed=false},QuickLootRoll.Greed);
        Expect("unknown data left alone",QuickLootRoll.Nothing,f with {Known=false});
        Expect("weekly left alone",QuickLootRoll.Nothing,f with {Weekly=true});
        s.Items.Add(new(){Id=100,Roll=QuickLootRoll.Need});
        Expect("weekly outranks explicit item",QuickLootRoll.Nothing,f with {Weekly=true});
        s.ProtectWeekly=false;Expect("explicitly disabled weekly protection",QuickLootRoll.Need,f with {Weekly=true});s.ProtectWeekly=true;
        Expect("unique held cannot need",QuickLootRoll.Pass,f with {UniqueOwned=true});
        s.Duties.Add(new(){Id=20,Roll=QuickLootRoll.Pass});Expect("item outranks duty",QuickLootRoll.Need);
        s.Items[0].Enabled=false;Expect("disabled item leaves duty",QuickLootRoll.Pass);
        s.Duties[0].Roll=QuickLootRoll.Nothing;Expect("duty nothing",QuickLootRoll.Nothing);
        s.Items[0].Enabled=true;s.Items[0].Roll=QuickLootRoll.Greed;Expect("item overrides duty nothing",QuickLootRoll.Greed);
        s.Items.Clear();s.Duties.Clear();
        s.MinimumLevel=true;s.Level=600;Expect("item level filter",QuickLootRoll.Pass);
        Expect("glamour bypasses global filter",QuickLootRoll.Need,f with {Glamour=true,ItemLevel=1});
        Expect("explicit pass still passes glamour",QuickLootRoll.Pass,f with {Glamour=true},QuickLootRoll.Pass);
        s.Items.Add(new(){Id=100,Roll=QuickLootRoll.Pass});Expect("explicit override beats glamour protection",QuickLootRoll.Pass,f with {Glamour=true});s.Items.Clear();
        s.MinimumLevel=false;s.OtherJobs=true;Expect("other job",QuickLootRoll.Pass,f with {FitsJob=false});Expect("unknown job kept",QuickLootRoll.Need,f with {FitsJob=null});
        s.OtherJobs=false;s.BelowAverage=true;s.BelowAverageBy=30;Expect("below average greed",QuickLootRoll.Greed);s.BelowAverageRoll=QuickLootRoll.Pass;Expect("below average pass",QuickLootRoll.Pass);
        Expect("average boundary allowed",QuickLootRoll.Need,f with {ItemLevel=570});Expect("unknown average kept",QuickLootRoll.Need,f with {AverageLevel=0});
        s.BelowAverage=false;s.NotUpgrade=true;Expect("not upgrade greed",QuickLootRoll.Greed);Expect("equal item level not upgrade",QuickLootRoll.Greed,f with {EquippedLevel=500});Expect("higher item level upgrade",QuickLootRoll.Need,f with {EquippedLevel=499});Expect("empty slot kept",QuickLootRoll.Need,f with {EquippedLevel=null});s.NotUpgrade=false;
        s.MinimumSeals=true;s.Seals=1600;Expect("low seals pass",QuickLootRoll.Pass);Expect("unknown seals kept",QuickLootRoll.Need,f with {SealValue=null});Expect("seals boundary kept",QuickLootRoll.Need,f with {SealValue=1600});s.MinimumSeals=false;
        foreach(var category in QuickLootSettings.Categories)
        {
            s.Collections.Clear();s.Collections[category]=new(){Enabled=true};
            var collectible=f with {Equipment=false,Category=category=="All unlockables"?"Mounts":category,Unlocked=true};
            Expect(category+" unlocked passes",QuickLootRoll.Pass,collectible);Expect(category+" locked kept",QuickLootRoll.Need,collectible with {Unlocked=false});
            s.Collections[category].UntradeableOnly=true;Expect(category+" tradeable kept",QuickLootRoll.Need,collectible);Expect(category+" untradeable passes",QuickLootRoll.Pass,collectible with {Untradeable=true});
        }
        s.Collections.Clear();s.Items.Add(new(){Id=100,Roll=QuickLootRoll.Nothing});Expect("nothing never changes to pass for duplicate",QuickLootRoll.Nothing,f with {UniqueOwned=true});
        var json=QuickLootPolicy.ExportRules(s.Items);check("QuickLoot rule roundtrip",QuickLootPolicy.ExportRules(QuickLootPolicy.ImportRules(json)),json);
        foreach(var invalid in new[]{"null","[null]","[{\"Id\":0}]","[{\"Id\":1,\"Roll\":99}]","[{\"Id\":1},{\"Id\":1}]"})
        {var rejected=false;try{QuickLootPolicy.ImportRules(invalid);}catch(Exception){rejected=true;}check("QuickLoot rejects invalid import "+invalid,rejected.ToString(),"True");}
        s.AutoMin=float.NaN;s.AutoMax=float.PositiveInfinity;check("QuickLoot invalid timing finite",double.IsFinite(QuickLootPolicy.Delay(s,true,.5)).ToString(),"True");
        s.ManualMin=2;s.ManualMax=4;check("QuickLoot delay minimum",QuickLootPolicy.Delay(s,false,0).ToString(),"2");check("QuickLoot delay maximum",QuickLootPolicy.Delay(s,false,1).ToString(),"4");
        var order=TabOrderPolicy.Reconcile(["housing","quickloot","settings"],["housing","settings"],false);
        check("QuickLoot hidden tab keeps remembered order",order.Contains("quickloot").ToString(),"True");
        check("QuickLoot settings stays last",TabOrderPolicy.Reconcile(order,["housing","quickloot","settings"],false).Last(),"settings");
    }
}
