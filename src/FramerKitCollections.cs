using FFXIVClientStructs.FFXIV.Client.Game.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private FramerKitUnlockReader? framerKitUnlockReader;
    private FramerKitUnlockReader FramerKitReader=>framerKitUnlockReader??=new(item=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(item)?.AdditionalData.RowId);
    private unsafe bool? ReadFramerKitUnlock(uint item)
    {
        var state=UIState.Instance();if(state==null||!state->PlayerState.IsLoaded)return null;
        return FramerKitReader.Read(item,kit=>state->PlayerState.IsFramersKitUnlocked(kit));
    }
}
