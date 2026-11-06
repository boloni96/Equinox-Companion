namespace EquinoxCompanion;
public static class CharacterLoginWorldPolicy
{
    // World Visit keeps the character on the home-world selection list.
    // Data Center Travel places it on the visiting-world selection list.
    public static string Resolve(string home,string current,uint homeDc,uint currentDc)=>
        homeDc!=0&&currentDc!=0&&homeDc!=currentDc&&!string.IsNullOrWhiteSpace(current)?current:home;
}
