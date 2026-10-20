using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class HistoricalHeld219Tests
{
    [ModuleInitializer] public static void Run()
    {
        void Check(bool value,string why){if(!value)throw new Exception("HistoricalHeld219: "+why);}
        var now=DateTimeOffset.Parse("2026-10-20T20:00:00Z");
        var actor=new Actor("12345","Example Gardener",57,57,"Siren","Siren");
        var placeholder=new FreeCompanyDetails("16140901064495857664","Example Company","TEST",57,"Example Gardener");
        var old=new SyncEvent("11111111111111111111111111111111","company.observed",now.AddDays(-10),actor,null,Company:placeholder);
        Check(SyncValidation.HistoricalCompanyPlaceholder(old,now),"old known placeholder capture is reported as historical");
        Check(!SyncValidation.CanSend(old,now),"reporting never makes placeholder eligible for upload");
        var placard=old with {Kind="house.placard",Company=null,House=new("Free Company house","Small","observed-placard",placeholder,"Example Estate","Example Company")};
        Check(SyncValidation.HistoricalCompanyPlaceholder(placard,now),"old placard placeholder is retained under the same historical label");
        Check(!SyncValidation.HistoricalCompanyPlaceholder(placard with {House=placard.House! with {FreeCompany=null}},now),"private or unrelated placard failure still needs review");
        Check(!SyncValidation.HistoricalCompanyPlaceholder(old with {At=now.AddMinutes(-10)},now),"fresh placeholder still needs attention");
        Check(!SyncValidation.HistoricalCompanyPlaceholder(old with {At=now.AddDays(1)},now),"future clock error remains actionable");
        Check(!SyncValidation.HistoricalCompanyPlaceholder(old with {Actor=actor with {HomeWorldId=0}},now),"missing character identity remains actionable");
        Check(!SyncValidation.HistoricalCompanyPlaceholder(old with {Company=placeholder with {Id="123456"}},now),"other incomplete FC captures remain actionable");
        Check(!SyncValidation.HistoricalCompanyPlaceholder(old with {Kind="garden.tended"},now),"garden actions are never classified as historical FC failures");
        Check(!SyncValidation.HistoricalCompanyPlaceholder(old with {Kind="housing.lottery"},now),"lottery and refund actions retain their attention rules");
        Check(SyncValidation.ActionableHeld(15,15)==0,"historical captures alone cannot keep sync orange");
        Check(SyncValidation.ActionableHeld(16,15)==1,"one new held action still triggers review");
        Check(SyncValidation.ActionableHeld(1,15)==0,"counts stay nonnegative during queue transitions");
        Console.WriteLine("PASS 219 historical placeholder reporting preserves fresh failures, garden and lottery actions, and upload validation");
    }
}
