using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe bool DispatchIndependentQuestAction(JsonElement step,string kind,ulong target)
    {
        if(kind=="UseItem"){
            var id=IndependentStepPolicy.Number(step,"ItemId");if(id==0)return false;
            var manager=ActionManager.Instance();if(manager==null)return false;
            var eventItem=DataManager.GetExcelSheet<Lumina.Excel.Sheets.EventItem>().GetRowOrDefault(id);
            var actionType=eventItem.HasValue?ActionType.EventItem:ActionType.Item;
            if(actionType==ActionType.Item&&!DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id).HasValue)return false;
            return manager->UseAction(actionType,id,target);
        }
        if(kind=="Emote"){
            var id=independentBook!.EmoteId(IndependentStepPolicy.Text(step,"Emote"));
            if(id==0||id>ushort.MaxValue)return false;
            var manager=EmoteManager.Instance();return manager!=null&&manager->CanExecuteEmote((ushort)id)&&manager->ExecuteEmote((ushort)id);
        }
        if(kind=="Say"&&step.TryGetProperty("ChatMessage",out var message)){
            // Resolve the quest's localized phrase; never execute route text as a command.
            var key=IndependentStepPolicy.Text(message,"Key");if(key.Length==0)return false;
            using var reference=JsonDocument.Parse(JsonSerializer.Serialize(new {ExcelSheet=IndependentStepPolicy.Text(message,"ExcelSheet"),Prompt=key}));
            var phrase=IndependentDialogueText(reference.RootElement,"Prompt");
            if(string.IsNullOrWhiteSpace(phrase)||phrase.Length>300||phrase.Any(c=>char.IsControl(c))||phrase.Contains('<')||phrase.Contains('>'))return false;
            var ui=UIModule.Instance();if(ui==null)return false;
            using var text=new Utf8String();text.SetString("/say "+phrase);ui->ProcessChatBoxEntry(&text,0,false);return true;
        }
        return false;
    }
}
