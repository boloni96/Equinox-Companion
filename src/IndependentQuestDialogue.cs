using System.Text.Json;
using System.Text.RegularExpressions;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string IndependentDialogueText(JsonElement choice,string field)
    {
        if(!choice.TryGetProperty(field,out var reference))return "";
        var sheetName=IndependentStepPolicy.Text(choice,"ExcelSheet");
        try{
            if(reference.ValueKind==JsonValueKind.Number&&sheetName=="Addon")return DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(reference.GetUInt32())?.Text.ToString()??"";
            var quest=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(65536+independentQuestId);
            if(!quest.HasValue)return "";
            if(sheetName.Length==0)sheetName=$"quest/{independentQuestId/100:000}/{quest.Value.Id}";
            var sheet=DataManager.GetExcelSheet<NativeEventQuestText>(name:sheetName);
            if(reference.ValueKind==JsonValueKind.Number)return sheet.GetRowOrDefault(reference.GetUInt32())?.Text??"";
            if(reference.ValueKind!=JsonValueKind.String)return "";
            var key=reference.GetString();foreach(var row in sheet)if(row.Key==key)return row.Text;
        }catch(Exception e){Log.Debug(e,"Independent quest dialogue reference unavailable");}
        return "";
    }
    private static bool IndependentTextMatches(string expected,string actual,bool pattern)
    {
        if(string.IsNullOrWhiteSpace(expected))return false;
        if(NativeEventPolicy.Prompt(expected,actual))return true;
        if(!pattern)return false;
        try{return Regex.IsMatch(actual,expected,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));}catch{return false;}
    }
    private unsafe bool IndependentQuestDialogue(JsonElement step,string kind,DateTimeOffset now)
    {
        if(!QuestConversationVisible()&&!VisibleFollowAddon("SelectYesno")&&!VisibleFollowAddon("CutSceneSelectString"))return false;
        StopIndependentWalk("Paused for quest conversation");
        if(now-independentActionAt<TimeSpan.FromMilliseconds(750))return true;
        if(!independentScene){
            if(independentNpc!=0&&Targets.Target?.BaseId==independentNpc&&independentStepSent&&now-independentActionAt<TimeSpan.FromSeconds(20))
                foreach(var name in new[]{"SelectString","SelectIconString","CutSceneSelectString"}){
                    var addon=KeyboundAddon(name);var choices=HelperChoices(addon,name);
                    var matches=choices.Select((text,index)=>(text,index)).Where(x=>x.text.Trim()==QuestTitle(independentQuestId)).ToArray();
                    if(matches.Length==1){SelectTravelChoice(addon,matches[0].index);independentActionAt=now;return true;}
                }
            questToolsStatus+=" · waiting for a verified quest conversation";return true;
        }
        if(VisibleFollowAddon("JournalAccept")){
            var a=(AddonJournalAccept*)KeyboundAddon("JournalAccept");
            if(kind=="AcceptQuest"&&NativeEventTitle((AtkUnitBase*)a,independentQuestId)&&NativeEventButton((AtkUnitBase*)a,a->AcceptButton))independentActionAt=now;
            return true;
        }
        if(VisibleFollowAddon("JournalResult")){
            var a=(AddonJournalResult*)KeyboundAddon("JournalResult");
            if(kind=="CompleteQuest"&&NativeEventTitle((AtkUnitBase*)a,independentQuestId)){
                if(!PrepareQuestReward(a,independentQuestId,out var rewardStatus)){questToolsStatus=rewardStatus;return true;}
                if(NativeEventButton((AtkUnitBase*)a,a->CompleteButton))independentActionAt=now;
            }
            else questToolsStatus+=" · choose any required quest reward manually";
            return true;
        }
        if(VisibleFollowAddon("Talk")){helperReplaying=true;try{ClickVisibleTalk();}finally{helperReplaying=false;}independentActionAt=now;return true;}
        if(step.TryGetProperty("DialogueChoices",out var definitions)&&definitions.ValueKind==JsonValueKind.Array)
            foreach(var definition in definitions.EnumerateArray()){
                var dataId=IndependentStepPolicy.Number(definition,"DataId");if(dataId!=0&&dataId!=independentNpc)continue;
                var expected=IndependentDialogueText(definition,"Prompt");
                bool pattern=definition.TryGetProperty("PromptIsRegularExpression",out var regex)&&regex.ValueKind==JsonValueKind.True;
                if(IndependentStepPolicy.Text(definition,"Type")=="YesNo"&&VisibleFollowAddon("SelectYesno")){
                    var a=(AddonSelectYesno*)KeyboundAddon("SelectYesno");if(a==null||a->PromptText==null)continue;
                    if(!IndependentTextMatches(expected,TravelMenuText(a->PromptText->NodeText.StringPtr)??"",pattern))continue;
                    var yes=!definition.TryGetProperty("Yes",out var answer)||answer.ValueKind==JsonValueKind.True;
                    if(NativeEventButton((AtkUnitBase*)a,yes?a->YesButton:a->NoButton)){independentActionAt=now;return true;}
                }
                if(IndependentStepPolicy.Text(definition,"Type")=="List"){
                    var answer=IndependentDialogueText(definition,"Answer");
                    bool answerPattern=definition.TryGetProperty("AnswerIsRegularExpression",out var re)&&re.ValueKind==JsonValueKind.True;
                    foreach(var name in new[]{"SelectString","SelectIconString","CutSceneSelectString"}){
                        var a=KeyboundAddon(name);if(a==null||!a->IsVisible||!a->IsReady)continue;
                        if(definition.TryGetProperty("Prompt",out var promptRef)&&promptRef.ValueKind!=JsonValueKind.Null){
                            // Match an actual displayed string; do not discard the imported prompt constraint.
                            if(a->AtkValues==null||a->AtkValuesCount>1024)continue;
                            var matched=false;
                            for(var i=0;i<a->AtkValuesCount;i++){
                                var v=a->AtkValues[i];if(((int)v.Type&15) is not (8 or 10))continue;
                                if(IndependentTextMatches(expected,TravelMenuText(v.String.Value)??"",pattern)){matched=true;break;}
                            }
                            if(!matched)continue;
                        }
                        var choices=HelperChoices(a,name);
                        var matches=choices.Select((text,index)=>(text,index)).Where(x=>IndependentTextMatches(answer,x.text,answerPattern)).ToArray();
                        if(matches.Length==1){helperReplaying=true;try{SelectTravelChoice(a,matches[0].index);}finally{helperReplaying=false;}independentActionAt=now;return true;}
                    }
                }
            }
        questToolsStatus+=" · unrecognized dialogue; choose manually";return true;
    }
}
