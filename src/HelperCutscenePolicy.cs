namespace EquinoxCompanion;
public static class HelperCutscenePolicy
{
    // English menus only, consistent with the existing quest/menu matching.
    public static bool Menu(string prompt,IReadOnlyList<string> choices)=>
        prompt=="Skip cutscene?"&&choices.Count==2&&choices[0]=="Yes."&&choices[1]=="No.";
}

public static class HelperQuestTextPolicy
{
    public static string Canonical(string scene,string speaker,string text)=>
        scene=="68694:1"&&speaker=="Kipih Jakkya"&&
        (text=="Greetings, adventurer! It's been a while. ...Hm? Do not tell me you've forgotten about Kipih Jakkya, reporter for The Raven?"||
         text=="Greetings, adventurer. I'm Kipih Jakkya, reporter for The Raven, Gridania's leading tabloid. I'm hoping that I can rely on your kind assistance.")
        ?"[Kipih Jakkya: The Man in Black introduction]":text;
}

public static class HelperCutsceneReplayPolicy
{
    public static readonly string[] BlockingAddons=["SelectString","SelectYesno","SelectIconString","CutSceneSelectString","JournalAccept","JournalResult","DifficultySelectYesNo"];
    public static int QuietMilliseconds(IEnumerable<HelperAction> steps)=>
        steps.LastOrDefault()?.Kind is "interact" or "completeQuest"?15000:2000;
    public static int MatchingLaterTalk(IReadOnlyList<HelperAction> steps,string scene,string text,string signature)
    {
        if(steps.Count<2||steps[0].Kind!="talk"||scene.Length==0||text.Length==0||signature.Length==0)return -1;
        var matches=new List<int>();
        for(var i=0;i<Math.Min(steps.Count,16);i++){
            var a=steps[i];
            if(a.Kind!="talk"||a.Scene!=scene||a.Npc!=steps[0].Npc)break;
            if(a.Text==text&&a.Signature==signature)matches.Add(i);
        }
        return matches.Count==1&&matches[0]>0?matches[0]:-1;
    }
    public static HelperAction[] Playback(HelperAction[] steps,bool skipPermitted)
    {
        if(!skipPermitted)return steps;
        var result=new List<HelperAction>();
        for(var i=0;i<steps.Length;i++){
            var current=steps[i];
            if(current.Kind=="talk"&&current.Scene.Length>0){
                var next=i+1;
                while(next<steps.Length&&steps[next].Kind=="talk"&&steps[next].Scene==current.Scene&&steps[next].Npc==current.Npc)next++;
                if(next<steps.Length&&steps[next] is {Kind:"skip",Addon:"SelectString",Text:"Skip cutscene?"} skip&&
                   skip.Scene==current.Scene&&skip.Npc==current.Npc)continue;
            }
            result.Add(current);
        }
        return result.ToArray();
    }
}
