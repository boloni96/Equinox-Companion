using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private FreeCompanyDetails? companyCandidate, observedCompany;
    private DateTimeOffset companyCandidateAt, nextCompanySample, observedCompanyAt;
    private static string CompanyGrandName(byte id) => id switch {1=>"Maelstrom",2=>"Order of the Twin Adder",3=>"Immortal Flames",_=>""};
    private unsafe void ObserveCompanyProfile(DateTimeOffset now)
    {
        var profile=AgentFreeCompanyProfile.Instance();
        if(profile==null || !profile->IsAgentActive()){companyCandidate=null;observedCompany=null;return;}
        if(now<nextCompanySample)return;
        nextCompanySample=now.AddSeconds(1);
        var actor=ReadActor();if(!SyncValidation.ActorReady(actor))return;
        var world=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(profile->World)?.Name.ToString() ?? "";
        string? formed=null;
        if(profile->FoundationDate>0)formed=DateTimeOffset.FromUnixTimeSeconds(profile->FoundationDate).ToString("O");
        var details=new CompanyProfileDetails(profile->Rank,profile->MemberCount,"company-profile",world,formed,
            profile->Slogan.ToString(),CompanyGrandName((byte)profile->GrandCompany),profile->Profile.Recruitment.ToString(),
            profile->Profile.Active.ToString(),CompanyProfileIdentity.Flags((int)profile->Profile.Focus, ["Role-playing","Leveling","Casual","Hardcore","Guildhests","Trials","Dungeons","Raids","PvP"]),CompanyProfileIdentity.Flags((int)profile->Profile.Seeking,["Tank","Healer","DPS","Crafter","Gatherer"]),profile->EstateName.ToString());
        var proxy = InfoProxyFreeCompany.Instance();
        var identity = proxy == null ? null : new FreeCompanyDetails(proxy->Id.ToString(System.Globalization.CultureInfo.InvariantCulture), proxy->NameString, "", proxy->HomeWorldId, proxy->MasterString);
        var id = CompanyProfileIdentity.Resolve(profile->RequestId, profile->Name.ToString(), profile->World, profile->Master.ToString(), identity);
        var company=new FreeCompanyDetails(id,profile->Name.ToString(),profile->Tag.ToString(),profile->World,profile->Master.ToString(),details);
        if(!SyncValidation.CompanyReady(company)){companyCandidate=null;observedCompany=null;return;}
        if(companyCandidate!=company){companyCandidate=company;companyCandidateAt=now;observedCompany=null;return;}
        // Two matching snapshots allow the asynchronous window to finish loading.
        if(now-companyCandidateAt<TimeSpan.FromSeconds(1))return;
        observedCompany=company;observedCompanyAt=now;
        KeepDiscovery(new(Guid.NewGuid().ToString("N"),"company.observed",now,actor,null,Company:company));
        discoveryStatus=$"Company Profile read: {company.Name} · master {company.MasterName}";
    }
}
