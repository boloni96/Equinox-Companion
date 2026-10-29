using System.Runtime.CompilerServices;
using System.Text;
using EquinoxCompanion;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
internal static unsafe class RetainerSortMenu302Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(bool ok,string reason){if(!ok)throw new Exception("Retainer Sort: "+reason);}
        var sortBytes=Encoding.UTF8.GetBytes("Sort\0");var discardBytes=Encoding.UTF8.GetBytes("Discard\0");var pluginBytes=Encoding.UTF8.GetBytes("Plugin\0");
        fixed(byte* sort=sortBytes,discard=discardBytes,plugin=pluginBytes){
            var agent=stackalloc AgentInventoryContext[1];*agent=default;agent->ContextItemCount=2;
            agent->EventParams[0].Type=agent->EventParams[1].Type=AtkValueType.String;agent->EventParams[0].String.Value=discard;agent->EventParams[1].String.Value=sort;
            var labels=new HashSet<string>{"Sort"};Check(TradeItemShortcutNative.NativeEntry(agent,labels)==1,"selects Sort, never Discard");
            agent->ContextItemDisabledMask=2;Check(TradeItemShortcutNative.NativeEntry(agent,labels)==-1,"disabled Sort rejected");agent->ContextItemDisabledMask=0;
            agent->ContextItemSubmenuMask=2;Check(TradeItemShortcutNative.NativeEntry(agent,labels)==-1,"submenu is not clicked blindly");agent->ContextItemSubmenuMask=0;
            var values=stackalloc AtkValue[14];for(var i=0;i<14;i++)values[i]=default;
            values[0].Type=values[2].Type=values[3].Type=AtkValueType.UInt;values[0].UInt=3;
            for(var i=8;i<11;i++)values[i].Type=AtkValueType.String;
            values[8].String.Value=plugin;values[9].String.Value=discard;values[10].String.Value=sort;
            for(var i=11;i<14;i++)values[i].Type=AtkValueType.Int;
            Check(TradeItemShortcutNative.DisplayedEntry(values,14,"Sort")==2,"plugin entries do not shift action onto Discard");
            values[14-1].Int=1;Check(TradeItemShortcutNative.DisplayedEntry(values,14,"Sort")==-1,"display-disabled Sort rejected");
        }
        Console.WriteLine("PASS retainer Sort native menu identity and disabled action checks");
    }
}
