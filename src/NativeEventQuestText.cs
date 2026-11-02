using Lumina.Excel;
namespace EquinoxCompanion;

// Quest dialogue sheets have two string columns: resource key and localized text.
// The sheet name is supplied from the current game's Quest row at runtime.
[Sheet("EquinoxEventDialogue")]
internal readonly struct NativeEventQuestText : IExcelRow<NativeEventQuestText>
{
    private readonly ExcelPage page;
    private readonly uint offset;
    public uint RowId { get; }
    private NativeEventQuestText(ExcelPage page,uint offset,uint row){this.page=page;this.offset=offset;RowId=row;}
    internal string Key=>page.ReadString(offset,offset).ToString();
    internal string Text=>page.ReadString(offset+4,offset).ToString();
    ExcelPage IExcelRow<NativeEventQuestText>.ExcelPage=>page;
    uint IExcelRow<NativeEventQuestText>.RowOffset=>offset;
    static NativeEventQuestText IExcelRow<NativeEventQuestText>.Create(ExcelPage page,uint offset,uint row)=>new(page,offset,row);
}
public sealed partial class Plugin
{
    private string ReadNativeEventPrompt(){
        try{
            var quest=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(NativeEventPolicy.FullQuest);
            if(!quest.HasValue)return "";
            var sheet=DataManager.GetExcelSheet<NativeEventQuestText>(name:$"quest/{NativeEventPolicy.Quest/100:000}/{quest.Value.Id}");
            foreach(var row in sheet)if(row.Key=="TEXT_SUBGSC801_05466_YESNOCHECK_000_029")return row.Text;
        }catch(Exception e){Log.Debug(e,"Event quest prompt unavailable; confirmation requires manual input");}
        return "";
    }
}
