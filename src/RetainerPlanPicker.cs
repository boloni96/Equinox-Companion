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
        if(duplicateExclusionsOpen)return;
        var io=ImGui.GetIO();var exists=retrievalItems.Any(x=>x.ItemId==item);string message;
        if(io.KeyShift&&!io.KeyAlt&&!exists){AddRetrieval([new(item,name,retrievalQuantity,recipe)]);message="Added "+name;}
        else if(io.KeyAlt&&!io.KeyShift&&exists){retrievalItems=retrievalItems.Where(x=>x.ItemId!=item).ToArray();retrievalSelected.Remove(item);retrievalPreview=[];retrievalPreviewDirty=true;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);message="Removed "+name;}
        else return;
        RetrievalNotifications.AddNotification(new Notification{Title=retrievalName,Content=message,MinimizedText=message,Type=NotificationType.Info,InitialDuration=TimeSpan.FromSeconds(1.5),HardExpiry=DateTime.UtcNow.AddSeconds(2)});
    }
    private unsafe void DrawRetrievalPicker()
    {
        if(retrieverWindow.IsOpen)ImGui.TextDisabled("Inventory shortcuts active · Shift add / Alt remove");
        else ImGui.Checkbox("Pick inventory items · Shift add / Alt remove",ref retrievalPickItems);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("While Organizer is open, hover a game inventory item: Shift selects its available bag quantity; Alt removes it. Picks appear in Inventory as a temporary selection. Saved plans are unchanged. Shortcuts pause during transfers and text entry.");
    }
    private unsafe void TickRetrievalPicker()
    {
        var focused=ShortcutKeyboard.OwnGameFocused();var ui=RaptureAtkModule.Instance();
        var shift=ShortcutKeyboard.Down(0x10,focused);var alt=ShortcutKeyboard.Down(0x12,focused);
        var blocked=(!duplicateExclusionsOpen&&!retrieverWindow.IsOpen&&!retrievalPickItems)||!Player.IsLoaded||!focused||gearRouting||armoireRunning||craftingWithdrawalPending||sellerRun.Running||TraderRunning||retrievalRun.Running||organizeRunning||chestRunning||retainerGilRunning||ImGui.GetIO().WantTextInput||ImGui.IsAnyItemActive()||ui==null||ui->IsTextInputActive()||ShortcutKeyboard.Down(0x11,focused);
        var action=shift&&!alt?1:alt&&!shift?-1:0;
        var item=ItemContextPolicy.Normalize(GameGui.HoveredItem);
        if(blocked||action==0||item==0){retrievalPickLast=0;retrievalPickAction=0;return;}
        if(item==retrievalPickLast&&action==retrievalPickAction)return;
        retrievalPickLast=item;retrievalPickAction=action;
        if(retrievalCatalog==null||!retrievalCatalog.Names.TryGetValue(item,out var name))return;
        if(duplicateExclusionsOpen){
            var changed=action>0?config.DuplicateDepositExclusions.Add(item):config.DuplicateDepositExclusions.Remove(item);
            if(changed){
                SaveConfiguration();var notice=(action>0?"Excluded ":"Allowed duplicates for ")+name;
                RetrievalNotifications.AddNotification(new Notification{Title="Duplicate exclusions",Content=notice,MinimizedText=notice,Type=NotificationType.Info,InitialDuration=TimeSpan.FromSeconds(1.5),HardExpiry=DateTime.UtcNow.AddSeconds(2)});
            }
            return;
        }
        EnsureMyInventoryCharacter();
        RefreshGearSetProtection();
        if(action>0&&OrganizerGearProtected(item)){retrievalError=OrganizerGearProtectionLabel(item);return;}
        var stock=action>0?OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)):Array.Empty<ChestStack>();
        if(!TemporaryInventorySelection.Pick(myInventorySelected,item,action,stock))return;
        myInventoryRefresh=default;
        var message=(action>0?"Selected ":"Removed ")+name;
        RetrievalNotifications.AddNotification(new Notification{Title="Temporary inventory selection",Content=message,MinimizedText=message,Type=NotificationType.Info,InitialDuration=TimeSpan.FromSeconds(1.5),HardExpiry=DateTime.UtcNow.AddSeconds(2)});
    }
}
