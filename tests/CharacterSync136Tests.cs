using EquinoxCompanion;
internal static class CharacterSync136Tests
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Run()
    {
        var valid = new FreeCompanyDetails("123", "Test FC", "TEST", 74, "Test Master");
        if(SyncValidation.CompleteCompany(valid) != valid) throw new Exception("Valid FC lost");
        foreach(var invalid in new[] { valid with { Id = "16140901064495857664" }, valid with { Name = "" }, valid with { WorldId = 0 }, valid with { Tag = new string('x',11) } })
        {
            if(SyncValidation.CompleteCompany(invalid) is not null) throw new Exception("Incomplete FC retained");
            var c = new CharacterDetails(23,"BRD",90,90,"Hyur","Midlander","Female",[new(23,"BRD",90)],SyncValidation.CompleteCompany(invalid),90,GrandCompany:"Order of the Twin Adder / Serpent Sergeant Third Class");
            if(!SyncValidation.CharacterReady(c)) throw new Exception("Optional FC blocked character");
            if(SyncValidation.CharacterReady(c with {Level=0})) throw new Exception("Invalid character accepted");
        }
        Console.WriteLine("PASS 136 incomplete optional FC isolated; valid FC retained; character validation preserved");
    }
}
