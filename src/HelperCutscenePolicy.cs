namespace EquinoxCompanion;
public static class HelperCutscenePolicy
{
    // English menus only, consistent with the existing quest/menu matching.
    public static bool Menu(string prompt,IReadOnlyList<string> choices)=>
        prompt=="Skip cutscene?"&&choices.Count==2&&choices[0]=="Yes"&&choices[1]=="No";
}

public static class HelperQuestTextPolicy
{
    public static string Canonical(string scene,string speaker,string text)=>
        scene=="68694:1"&&speaker=="Kipih Jakkya"&&
        (text=="Greetings, adventurer! It's been a while. ...Hm? Do not tell me you've forgotten about Kipih Jakkya, reporter for The Raven?"||
         text=="Greetings, adventurer. I'm Kipih Jakkya, reporter for The Raven, Gridania's leading tabloid. I'm hoping that I can rely on your kind assistance.")
        ?"[Kipih Jakkya: The Man in Black introduction]":text;
}
