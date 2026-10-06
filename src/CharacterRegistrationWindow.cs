using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private NewCharacterWindow registrationWindow=null!;
    private string registrationSession="",registrationPerson="",registrationPersonName="",registrationNewAccount="",registrationFreshKey="";
    private bool registrationDeferred,registrationNewPersonMode,registrationNewAccountMode;
    private SyncEvent? registrationSnapshot;
    private DateTimeOffset registrationStartedAt;
    private readonly HashSet<string> registrationNotified=[];
    private bool RegistrationNeedsRoster => Player.IsLoaded && (registrationSession!=config.PairingKey+":"+Player.ContentId || registrationFreshKey!=config.PairingKey || registrationWindow.IsOpen || config.Discoveries.Any(e=>e.Kind=="character.registered"&&e.Actor.ContentId==Player.ContentId.ToString()&&e.Registration?.PairingScope==RegistrationScope&&!registrationNotified.Contains(e.Id)));
    private string RegistrationScope => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.PairingKey))).ToLowerInvariant();
    private void UpdateCharacterRegistration(DateTimeOffset now)
    {
        if(!Player.IsLoaded||Player.ContentId==0||!config.SyncEnabled||!config.SyncCharacterDetails||config.PairingKey.Length!=64){registrationWindow.IsOpen=false;registrationSession="";minimizedLaunchers.Remove("New Character");return;}
        var session=config.PairingKey+":"+Player.ContentId;
        if(session!=registrationSession){minimizedLaunchers.Remove("New Character");registrationSession=session;registrationStartedAt=now;nextDiscovery=default;nextCharacterRefresh=default;registrationDeferred=false;registrationPerson="";registrationPersonName="";registrationNewAccount="";registrationNewPersonMode=false;registrationNewAccountMode=false;registrationSnapshot=null;registrationWindow.IsOpen=false;nextRosterRead=default;}
        if(now<characterReadyAt||registrationFreshKey!=config.PairingKey||config.SharedRoster is not {ProtocolVersion:>=14} roster)return;
        var actor=ReadActor();
        if(!SyncValidation.ActorReady(actor))return;
        var pending=config.Discoveries.LastOrDefault(e=>e.Kind=="character.registered"&&e.Actor.ContentId==actor.ContentId&&e.Registration?.PairingScope==RegistrationScope);
        if(CharacterRegistrationPolicy.Find(roster,actor) is {} known){
            registrationWindow.IsOpen=false;minimizedLaunchers.Remove("New Character");
            if(pending is not null&&registrationNotified.Add(pending.Id))Chat.Print($"[Equinox] {known.Name} added to the Journal · {roster.People.First(p=>p.Characters.Contains(known)).Name} / {known.Account}.");
            return;
        }
        if(pending is not null){registrationWindow.IsOpen=false;minimizedLaunchers.Remove("New Character");return;}
        registrationSnapshot=config.Discoveries.LastOrDefault(e=>e.Kind=="character.updated"&&e.Actor.ContentId==actor.ContentId&&e.At>=registrationStartedAt&&e.At>=characterReadyAt.AddSeconds(-15)&&SyncValidation.CharacterReady(e.Character));
        if(registrationSnapshot is null)return;
        if(CharacterRegistrationPolicy.Destination(roster,registrationSnapshot.Character!.AccountKey) is {} destination){SubmitCharacterRegistration(destination.Person,destination.Account);return;}
        if(!registrationDeferred)registrationWindow.IsOpen=true;
    }
    private void SubmitCharacterRegistration(string personId,string accountId,string personName="",string accountName="")
    {
        var snapshot=registrationSnapshot;
        if(snapshot is null||!Player.IsLoaded||snapshot.Actor.ContentId!=Player.ContentId.ToString()||registrationSession!=config.PairingKey+":"+Player.ContentId)return;
        var world=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(snapshot.Actor.HomeWorldId);
        var dc=world?.DataCenter.Value;
        var region=dc?.Region.RowId switch {1=>"Japan",2=>"North America",3=>"Europe",4=>"Oceania",_=>""};
        var registration=new CharacterRegistration(personId,accountId,personName.Trim(),accountName.Trim(),snapshot.Character?.AccountKey??"",dc?.Name.ToString()??"",region,RegistrationScope);
        if(!CharacterRegistrationPolicy.Valid(registration))return;
        KeepDiscovery(snapshot with {Id=Guid.NewGuid().ToString("N"),Kind="character.registered",At=DateTimeOffset.UtcNow,Registration=registration},true);
        nextSync=default;nextRosterRead=default;registrationWindow.IsOpen=false;minimizedLaunchers.Remove("New Character");
        Chat.Print($"[Equinox] Sending {snapshot.Actor.Name} to your Journal. It will retry automatically if the connection is unavailable.");
    }
    private sealed class NewCharacterWindow:Window
    {
        private readonly Plugin p;
        public NewCharacterWindow(Plugin p):base("New Character Detected###EquinoxNewCharacter",ImGuiWindowFlags.AlwaysAutoResize){this.p=p;RespectCloseHotkey=false;SizeConstraints=new(){MinimumSize=new Vector2(430,200),MaximumSize=new Vector2(650,650)};}
        public override void OnClose()=>p.registrationDeferred=true;
        public override void PostDraw()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("New Character"));
        public override void Draw()
        {
            var snapshot=p.registrationSnapshot;var roster=p.config.SharedRoster;
            if(snapshot is null||roster is null||!Player.IsLoaded||snapshot.Actor.ContentId!=Player.ContentId.ToString()){IsOpen=false;return;}
            ImGui.TextUnformatted(snapshot.Actor.Name);ImGui.TextWrapped(HomeLocation(snapshot.Actor));ImGui.Separator();
            ImGui.TextWrapped("Choose Person, then Account. Selecting the account sends automatically.");
            if(ImGui.BeginCombo("Person",p.registrationNewPersonMode?"New Person":roster.People.FirstOrDefault(x=>x.Id==p.registrationPerson)?.Name??(p.registrationPersonName.Length>0?p.registrationPersonName:"Choose Person"))){
                foreach(var person in roster.People)if(ImGui.Selectable(person.Name+"##"+person.Id)){p.registrationPerson=person.Id;p.registrationPersonName="";p.registrationNewPersonMode=false;p.registrationNewAccountMode=false;p.registrationNewAccount="";}
                if(ImGui.Selectable("+ New Person")){p.registrationPerson="";p.registrationPersonName="";p.registrationNewPersonMode=true;p.registrationNewAccountMode=false;}
                ImGui.EndCombo();
            }
            if(p.registrationNewPersonMode){
                if(ImGui.InputText("Person name (Enter)",ref p.registrationPersonName,100,ImGuiInputTextFlags.EnterReturnsTrue)&&!string.IsNullOrWhiteSpace(p.registrationPersonName)&&!p.registrationPersonName.Any(char.IsControl)){p.registrationPerson="companion-person-"+Guid.NewGuid().ToString("N");p.registrationNewPersonMode=false;}
            }else if(p.registrationPerson.Length>0){
                var person=roster.People.FirstOrDefault(x=>x.Id==p.registrationPerson);
                var accounts=person?.Accounts??person?.Characters.Select(c=>new SharedAccount(c.AccountId,c.Account)).DistinctBy(a=>a.Id).ToArray()??[];
                if(ImGui.BeginCombo("Account",p.registrationNewAccountMode?"New Account":"Choose Account")){
                    foreach(var a in accounts)if(ImGui.Selectable(a.Name+"##"+a.Id))p.SubmitCharacterRegistration(p.registrationPerson,a.Id,p.registrationPersonName);
                    if(ImGui.Selectable("+ New Account")){p.registrationNewAccountMode=true;p.registrationNewAccount="";}
                    ImGui.EndCombo();
                }
                if(p.registrationNewAccountMode&&ImGui.InputText("Account name (Enter to send)",ref p.registrationNewAccount,100,ImGuiInputTextFlags.EnterReturnsTrue)&&!string.IsNullOrWhiteSpace(p.registrationNewAccount))p.SubmitCharacterRegistration(p.registrationPerson,"companion-account-"+Guid.NewGuid().ToString("N"),p.registrationPersonName,p.registrationNewAccount);
            }
            if(ImGui.Button("Later")){p.registrationDeferred=true;IsOpen=false;}
            ImGui.TextDisabled("Reopen with /equinox register.");
        }
    }
}
