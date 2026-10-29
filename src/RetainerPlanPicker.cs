using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    [PluginService] internal static INotificationManager RetrievalNotifications { get; private set; }=null!;
    private bool retrievalPickItems;private uint retrievalPickLast;private int retrievalPickAction;
    private void PickOrganizerRow(uint item,string name,uint recipe=0)
    {
        if(!ImGui.IsItemHovered()||ImGui.GetIO().WantTextInput||craftingWithdrawalPending||retrievalRun.Running||organizeRunning||chestRunning)return;
        var io=ImGui.GetIO();var exists=retrievalItems.Any(x=>x.ItemId==item);string message;
        if(io.KeyShift&&!io.KeyAlt&&!exists){AddRetrieval([new(item,name,retrievalQuantity,recipe)]);message="Added "+name;}
        else if(io.KeyAlt&&!io.KeyShift&&exists){retrievalItems=retrievalItems.Where(x=>x.ItemId!=item).ToArray();retrievalSelected.Remove(item);retrievalPreview=[];retrievalPreviewDirty=true;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);message="Removed "+name;}
        else return;
        RetrievalNotifications.AddNotification(new Notification{Title=retrievalName,Content=message,MinimizedText=message,Type=NotificationType.Info,InitialDuration=TimeSpan.FromSeconds(1.5),HardExpiry=DateTime.UtcNow.AddSeconds(2)});
    }
    private unsafe void DrawRetrievalPicker()
    {
        ImGui.Checkbox("Pick items · Shift add / Alt remove",ref retrievalPickItems);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("While this plan is open, hover a game inventory item and hold Shift to add it or Alt to remove it. Adds a new item once, using Add quantity; existing quantities are kept. Selected saved plans update immediately; new lists use Save preset.");
        TickRetrievalPicker();
    }
    private unsafe void TickRetrievalPicker()
    {
        var focused=ShortcutKeyboard.OwnGameFocused();var ui=RaptureAtkModule.Instance();
        var shift=ShortcutKeyboard.Down(0x10,focused);var alt=ShortcutKeyboard.Down(0x12,focused);
        var blocked=!retrievalPickItems||!focused||retrievalRun.Running||organizeRunning||chestRunning||retainerGilRunning||ImGui.GetIO().WantTextInput||ImGui.IsAnyItemActive()||ui==null||ui->IsTextInputActive()||ShortcutKeyboard.Down(0x11,focused);
        var action=shift&&!alt?1:alt&&!shift?-1:0;
        var item=ItemContextPolicy.Normalize(GameGui.HoveredItem);
        if(blocked||action==0||item==0){retrievalPickLast=0;retrievalPickAction=0;return;}
        if(item==retrievalPickLast&&action==retrievalPickAction)return;
        retrievalPickLast=item;retrievalPickAction=action;
        if(retrievalCatalog==null||!retrievalCatalog.Names.TryGetValue(item,out var name))return;
        var exists=retrievalItems.Any(x=>x.ItemId==item);string message;
        if(action>0){if(exists)return;AddRetrieval([new(item,name,Math.Clamp(retrievalQuantity,1,RetainerRetrieval.MaxQuantity))]);message="Added "+name;}
        else{if(!exists)return;retrievalItems=retrievalItems.Where(x=>x.ItemId!=item).ToArray();retrievalSelected.Remove(item);retrievalPreview=[];retrievalPlan=[];retrievalRecipeChoice=0;retrievalPreviewDirty=true;message="Removed "+name;}
        if(action<0&&retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);
        RetrievalNotifications.AddNotification(new Notification{Title=retrievalName,Content=message,MinimizedText=message,Type=NotificationType.Info,InitialDuration=TimeSpan.FromSeconds(1.5),HardExpiry=DateTime.UtcNow.AddSeconds(2)});
    }
}
