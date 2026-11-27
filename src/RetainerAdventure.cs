using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private enum RetainerStep { Character, Switch, Travel, Bell, List, Seller, Leave }
    private sealed record RetainerStop(string Character, HashSet<ulong> Retainers);
    private sealed record RetainerAdventure(string Board, string Generation, string Pairing, Queue<RetainerStop> Stops, bool Private, bool Apply)
    {
        public RetainerStep Step;
        public bool BellPause, Walking;
        public DateTimeOffset Next, Deadline;
        public ulong Owner;
    }
    private RetainerAdventure? retainerAdventure;
    private string retainerAdventureStatus = "";
    private bool retainerAdventureUpdatePrices;

    private void DrawRetainerAdventure(AdventureBoard board)
    {
        if (!board.Stops.Any(s => s.Tasks.Any(t => t.Rule?.Kind == "market"))) return;
        if (ImGui.CollapsingHeader("Automatic retainers"))
        {
            ImGui.BeginDisabled(retainerAdventure != null);
            var location = config.RetainerAdventurePrivate ? 1 : 0;
            if (ImGui.Combo("Bell location", ref location, "FC workshop\0Private house\0"))
            {
                config.RetainerAdventurePrivate = location == 1;
                SaveConfiguration();
            }
            ImGui.TextWrapped(location == 1 ? "Place a summoning bell inside your private house." : "Uses the summoning bell inside your FC workshop.");
            var change = config.RetainerAdventureSwitch;
            if (ImGui.Checkbox("Switch assigned characters on this login", ref change))
            {
                config.RetainerAdventureSwitch = change;
                SaveConfiguration();
            }
            ImGui.Checkbox("Apply Seller price updates", ref retainerAdventureUpdatePrices);
            ImGui.BeginDisabled(!adventureFresh || !Player.IsLoaded || !config.Seller.Enabled);
            if (ImGui.Button("Start automatic retainers")) StartRetainerAdventure(board);
            ImGui.EndDisabled();
            ImGui.EndDisabled();
            if (!config.Seller.Enabled) ImGui.TextDisabled("Enable Market & Seller to run this adventure.");
        }
        if (retainerAdventure != null && ImGui.SmallButton("Stop automatic retainers")) StopRetainerAdventure("Stopped by you.");
        if (retainerAdventureStatus.Length > 0) ImGui.TextWrapped(retainerAdventureStatus);
    }

    private void StartRetainerAdventure(AdventureBoard board)
    {
        var current = CurrentSharedCharacter();
        if (current == null) { retainerAdventureStatus = "Waiting for the paired character."; return; }
        if (marketLoad != null || marketLoadFailed || !marketHooksEnabled) { retainerAdventureStatus = "Wait for Market to finish loading its records."; return; }
        if (OtherRetainerAdventureWork() || sellerRun.Running || estateTrip != null || estateHomeReturn != null || assignedLogin != null || independentMoving || nativeWorldTrip != null)
        { retainerAdventureStatus = "Finish the active operation first."; return; }
        RememberLoadedLoginAccount();
        var characters = config.SharedRoster!.People.First(p=>p.Characters.Any(c=>c.Id==current.Id)).Characters;
        var stops = board.Stops.SelectMany(s=>s.Tasks).Where(t=>!t.Done && t.Rule?.Kind=="market")
            .Select(t=>t.Rule!).Where(r=>ulong.TryParse(r.RetainerId,out var id)&&id!=0)
            .GroupBy(r=>r.Cid).Where(g=>g.Key==current.Id || config.RetainerAdventureSwitch &&
                characters.Any(c=>c.Id==g.Key && GardenServiceAccount(c)>=0))
            .OrderByDescending(g=>g.Key==current.Id)
            .Select(g=>new RetainerStop(g.Key,g.Select(r=>ulong.Parse(r.RetainerId!)).ToHashSet()));
        var queue = new Queue<RetainerStop>(stops);
        if (queue.Count==0) { retainerAdventureStatus="No pending retainer tasks for the selected characters on this login."; return; }
        retainerAdventure=new(board.Id,board.Generation,config.PairingKey,queue,config.RetainerAdventurePrivate,retainerAdventureUpdatePrices)
            { Owner=Player.ContentId, Deadline=DateTimeOffset.UtcNow.AddMinutes(2) };
        retainerAdventureStatus="Preparing assigned retainers…";
    }

    private bool OtherRetainerAdventureWork() => gardenTendRoute!=null || gardenCareRun?.Running==true || workshopQueueRun ||
        TraderRunning || organizeRunning || retrievalRun.Running || chestRunning || retainerGilRunning || followSession.Armed ||
        shoppingTravel!=null || independentQuestId!=0 || nativeHousingTrip!=null;

    private void StopRetainerAdventure(string message)
    {
        var run=retainerAdventure;
        retainerAdventure=null;
        if (run?.Step==RetainerStep.Switch) CancelAssignedLogin(message);
        if (run?.Step==RetainerStep.Travel) StopEstateTravel(message);
        if (run?.Walking==true) StopIndependentWalk(message);
        if (run?.Step==RetainerStep.Seller && sellerRun.Running) sellerRun.Stop(message);
        if (run?.BellPause==true) ReleaseSellerLocks();
        retainerAdventureStatus=message;
    }

    private unsafe void TickRetainerAdventure(DateTimeOffset now)
    {
        if (retainerAdventure is not {} run || now<run.Next) return;
        run.Next=now.AddMilliseconds(250);
        try
        {
            if (!config.SyncEnabled || !config.Seller.Enabled || config.PairingKey!=run.Pairing || adventureSelected!=run.Board ||
                OtherRetainerAdventureWork() || (sellerRun.Running && run.Step!=RetainerStep.Seller))
                throw new InvalidOperationException("Adventure, settings or active operation changed.");
            if (run.Step!=RetainerStep.Seller && now>run.Deadline) throw new InvalidOperationException("Timed out: "+retainerAdventureStatus);
            var board=adventureBoards.FirstOrDefault(b=>b.Id==run.Board && b.Generation==run.Generation);
            if (board==null || !adventureFresh) throw new InvalidOperationException("Adventure unavailable. Resume after sync.");
            if (Conditions[ConditionFlag.InCombat] || Conditions[ConditionFlag.BoundByDuty]) throw new InvalidOperationException("Stopped for combat or duty.");
            if (!Player.IsLoaded || Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51]) return;
            var stop=run.Stops.Peek();
            var current=CurrentSharedCharacter();
            if (current==null) return;
            if (run.Step==RetainerStep.Switch)
            {
                if (current.Id!=stop.Character || assignedLogin!=null)
                {
                    if (assignedLogin==null && assignedLoginError.Length>0) throw new InvalidOperationException(assignedLoginError);
                    return;
                }
                run.Owner=Player.ContentId;
                run.Step=RetainerStep.Character;
            }
            if (run.Owner!=Player.ContentId) throw new InvalidOperationException("The active character changed.");
            var destination=EstateTravelCommand.Parse(run.Private?"epriv":"ws")!.Value;
            if (run.Step==RetainerStep.Character)
            {
                stop.Retainers.IntersectWith(FilterAdventure(board).Stops.SelectMany(s=>s.Tasks)
                    .Where(t=>!t.Done && t.Rule?.Kind=="market" && t.Rule.Cid==stop.Character)
                    .Select(t=>ulong.TryParse(t.Rule!.RetainerId,out var id)?id:0UL));
                if (stop.Retainers.Count==0) { Advance(); return; }
                if (current.Id!=stop.Character)
                {
                    var target=config.SharedRoster!.People.SelectMany(p=>p.Characters).First(c=>c.Id==stop.Character);
                    if (!StartAssignedCharacterSwitch(target,allowLinkedAccounts:true)) throw new InvalidOperationException(characterTravelStatus);
                    run.Step=RetainerStep.Switch;run.Deadline=now.AddMinutes(6);retainerAdventureStatus="Switching to "+target.Name;return;
                }
                if (!AtDestination())
                {
                    StartEstateTravelCommand(destination);
                    if (estateTrip==null && estateHomeReturn==null) throw new InvalidOperationException("Estate travel could not start. Check the travel message.");
                }
                run.Step=RetainerStep.Travel;run.Deadline=now.AddMinutes(10);retainerAdventureStatus="Travelling to "+destination.Label;return;
            }
            if (run.Step==RetainerStep.Travel)
            {
                if (estateTrip!=null || estateHomeReturn!=null) return;
                if (!AtDestination()) throw new InvalidOperationException("The selected estate interior was not reached.");
                var pause=sellerAutoRetainerPause.Acquire(now);
                if (pause.Error.Length>0) { if(sellerAutoRetainerPause.Held) ReleaseSellerLocks(); throw new InvalidOperationException(pause.Error); }
                run.BellPause=true;run.Step=RetainerStep.Bell;run.Deadline=now.AddMinutes(2);retainerAdventureStatus="Finding the summoning bell…";
            }
            if (!AtDestination()) throw new InvalidOperationException("Left the selected estate.");
            if (run.Step is RetainerStep.Bell or RetainerStep.List)
            {
                var pause=sellerAutoRetainerPause.Check(now);
                if (pause.Error.Length>0) throw new InvalidOperationException(pause.Error);
                if (!pause.Ready) return;
                if (SellerAddon("RetainerList")!=null)
                {
                    if (ReadSellerRetainers().Length==0) return;
                    if (run.Walking) { StopIndependentWalk("Bell reached");run.Walking=false; }
                    StartSeller(run.Apply,stop.Retainers);
                    if (!sellerRun.Running) throw new InvalidOperationException(sellerNotice);
                    run.BellPause=false;run.Step=RetainerStep.Seller;retainerAdventureStatus="Seller · "+current.Name;return;
                }
                if (run.Step==RetainerStep.List) return;
                if (QuestConversationVisible() || VisibleFollowAddon("SelectYesno")) throw new InvalidOperationException("Close the open dialogue and resume.");
                var self=Objects.LocalPlayer;if(self==null)return;
                var bell=Objects.Where(o=>o.Address!=0 && o.IsTargetable && (o.ObjectKind is ObjectKind.EventObj or ObjectKind.EventNpc) &&
                    (string.Equals(o.Name.TextValue,"Summoning Bell",StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(DataManager.GetExcelSheet<Lumina.Excel.Sheets.EObjName>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(o.BaseId)?.Singular.ToString(),"summoning bell",StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(o=>Vector3.DistanceSquared(self.Position,o.Position)).FirstOrDefault();
                if (bell==null) { retainerAdventureStatus="Waiting for a summoning bell inside "+destination.Label;return; }
                if (Vector3.DistanceSquared(self.Position,bell.Position)>16)
                {
                    run.Walking=true;IndependentWalk(bell.Position,4,now,allowFlight:false,geometryOnly:true,objectRangeArrival:true);
                    retainerAdventureStatus="Walking to the summoning bell";return;
                }
                if (run.Walking) { StopIndependentWalk("Bell reached");run.Walking=false; }
                TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)bell.Address,false);
                run.Step=RetainerStep.List;run.Deadline=now.AddSeconds(20);retainerAdventureStatus="Opening the retainer list…";return;
            }
            if (run.Step==RetainerStep.Seller)
            {
                if (sellerRun.Running) { retainerAdventureStatus=sellerRun.Status;return; }
                if (!sellerRun.Status.StartsWith("Finished",StringComparison.Ordinal)) throw new InvalidOperationException(sellerRun.Status);
                adventureNext=default;
                if (run.Stops.Count>1 && SellerAddon("RetainerList")!=null) CloseSellerAddon("RetainerList");
                run.Step=RetainerStep.Leave;run.Deadline=now.AddSeconds(20);
            }
            if (run.Step==RetainerStep.Leave && (run.Stops.Count==1 || !Conditions[ConditionFlag.OccupiedSummoningBell])) Advance();

            bool AtDestination()
            {
                if (Player.CurrentWorld.RowId!=Player.HomeWorld.RowId) return false;
                var estate=AddressOf(EstateDestinationHouse(destination.Property));
                var inside=ReadEstateInterior(HousingManager.Instance());
                return estate!=null && inside!=null && EstateTravelCommand.InteriorArrived(destination.Mode,SameEstate(inside,estate),inside.Workshop,inside.Room);
            }
            void Advance()
            {
                run.Stops.Dequeue();
                if (run.Stops.Count==0) { retainerAdventure=null;retainerAdventureStatus="Retainer route finished. Journal confirmation follows the recorded checks.";return; }
                run.Step=RetainerStep.Character;run.Deadline=now.AddMinutes(2);
            }
        }
        catch(Exception ex) { StopRetainerAdventure(ex.Message); }
    }
}
