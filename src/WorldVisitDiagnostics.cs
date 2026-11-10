using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe object ReadWorldVisitControls(AtkUnitBase* addon)
    {
        var controls=new List<object>();var seen=new HashSet<nint>();var nodes=0;var truncated=false;
        var pending=new Queue<(nint Manager,uint Parent,int Depth)>();
        string Short(string? value){var s=value??"";return s[..Math.Min(160,s.Length)];}
        object ReadList(AtkComponentList* list){
            var rows=new List<object>();if(list==null)return new{available=false};
            var count=list->GetItemCount();
            if(count<0||count>256)return new{available=false,count};
            for(var i=0;i<Math.Min(count,16);i++){
                var renderer=list->GetItemRenderer(i);
                var text=renderer!=null&&renderer->ListItemIndex==i?TravelNodeText(&renderer->AtkComponentButton.AtkComponentBase.UldManager).Take(4).Select(Short).ToArray():[];
                rows.Add(new{index=i,label=Short(TravelMenuText(list->GetItemLabel(i).Value)),text,disabled=list->GetItemDisabledState(i)});
            }
            return new{available=true,count,selected=list->SelectedItemIndex,interactive=list->IsItemInteractionEnabled,truncated=count>16,rows};
        }
        void Walk(AtkUldManager* manager,uint parent,int depth){
            if(manager==null||manager->NodeList==null||manager->NodeListCount>512)return;
            for(var i=0;i<manager->NodeListCount;i++){
                if(nodes>=512||controls.Count>=64){truncated=true;return;}
                var node=manager->NodeList[i];nodes++;
                if(node==null||(int)node->Type<1000||!seen.Add((nint)node))continue;
                var component=((AtkComponentNode*)node)->Component;if(component==null)continue;
                var type=component->GetComponentType();object? state=null;
                if(type==ComponentType.DropDownList){
                    var drop=(AtkComponentDropDownList*)component;
                    state=new{selected=drop->GetSelectedItemIndex(),open=drop->IsOpen,list=ReadList(drop->List)};
                }else if(type==ComponentType.List)state=ReadList((AtkComponentList*)component);
                else if(type==ComponentType.Button||type==ComponentType.CheckBox){
                    var button=(AtkComponentButton*)component;
                    state=new{enabled=button->IsEnabled,text=TravelNodeText(&component->UldManager).Take(4).Select(Short).ToArray()};
                }
                if(state!=null)controls.Add(new{id=node->NodeId,parent,depth,type=type.ToString(),visible=node->IsVisible(),x=node->X,y=node->Y,width=node->Width,height=node->Height,state});
                if(depth<2)pending.Enqueue(((nint)(&component->UldManager),node->NodeId,depth+1));
            }
        }
        if(addon!=null)pending.Enqueue(((nint)(&addon->UldManager),0,0));
        while(pending.Count>0&&nodes<512&&controls.Count<64){
            var next=pending.Dequeue();Walk((AtkUldManager*)next.Manager,next.Parent,next.Depth);
        }
        if(pending.Count>0)truncated=true;
        return new{nodesScanned=nodes,truncated,controls};
    }
}
