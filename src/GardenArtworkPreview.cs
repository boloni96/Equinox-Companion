using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private string[]? gardenPreviewPaths;
    private string gardenPreviewSearch = "", gardenPreviewCategory = "icons", gardenPreviewSelected = "";
    private bool gardenFlowerPreviewRunning=true;
    private double gardenFlowerPreviewStarted=-1;
    private string gardenFlowerPreviewColour="white";
    private void DrawGardenInfoLabel(string icon, string label)
    {
        var texture=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art/assets/icons/"+icon+".png")).GetWrapOrDefault();
        if(texture is not null){ImGui.Image(texture.Handle,new Vector2(ImGui.GetTextLineHeight()));ImGui.SameLine();}
        ImGui.TextWrapped(label);
    }
    private void DrawGardenArtworkPreview()
    {
        if(!ImGui.CollapsingHeader("Garden artwork preview"))return;
        ImGui.TextWrapped("Preview only — these controls do not change plants, care times, observations or your shared journal. Some artwork still needs supported game observations before it can appear automatically.");
        DrawGardenFlowerPreview();
        if(gardenPreviewPaths is null)
        {
            var root=Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art");
            try{gardenPreviewPaths=Directory.EnumerateFiles(Path.Combine(root,"assets"),"*.png",SearchOption.AllDirectories).Select(p=>Path.GetRelativePath(root,p).Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray();}
            catch(Exception e){ImGui.TextWrapped("Artwork preview unavailable: "+e.Message);return;}
        }
        if(ImGui.BeginCombo("Artwork category",gardenPreviewCategory))
        {
            foreach(var category in new[]{"All"}.Concat(gardenPreviewPaths.Select(p=>p.Split('/')[1]).Distinct()))
                if(ImGui.Selectable(category,category==gardenPreviewCategory))gardenPreviewCategory=category;
            ImGui.EndCombo();
        }
        ImGui.InputText("Find artwork",ref gardenPreviewSearch,120);
        var choices=gardenPreviewPaths.Where(p=>(gardenPreviewCategory=="All"||p.Split('/')[1]==gardenPreviewCategory)&&p.Contains(gardenPreviewSearch,StringComparison.OrdinalIgnoreCase)).ToArray();
        ImGui.TextWrapped($"{choices.Length} matching images / {gardenPreviewPaths.Length} installed. Atlas and authoring previews are listed in the downloadable inventory.");
        if(choices.Length==0)return;
        var selected=Array.IndexOf(choices,gardenPreviewSelected);if(selected<0)selected=0;
        if(ImGui.BeginCombo("Artwork file",Path.GetFileName(choices[selected])))
        {
            for(var i=0;i<choices.Length;i++)if(ImGui.Selectable(choices[i],i==selected))selected=i;
            ImGui.EndCombo();
        }
        if(ImGui.SmallButton("Previous artwork"))selected=(selected+choices.Length-1)%choices.Length;
        ImGui.SameLine();if(ImGui.SmallButton("Next artwork"))selected=(selected+1)%choices.Length;
        gardenPreviewSelected=choices[selected];ImGui.TextWrapped(gardenPreviewSelected);
        var at=ImGui.GetCursorScreenPos();var size=Math.Min(160,ImGui.GetContentRegionAvail().X);
        ImGui.GetWindowDrawList().AddRectFilled(at,at+new Vector2(size),0xff453326);
        var image=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art",gardenPreviewSelected)).GetWrapOrDefault();
        if(image is not null)
        {
            var scale=Math.Min(size/image.Width,size/image.Height);var dimensions=new Vector2(image.Width,image.Height)*scale;
            var offset=(new Vector2(size)-dimensions)/2;
            ImGui.GetWindowDrawList().AddImage(image.Handle,at+offset,at+offset+dimensions);
        }
        else ImGui.TextUnformatted("Loading artwork…");
        ImGui.SetCursorScreenPos(at);ImGui.Dummy(new Vector2(size));
    }
    private void DrawGardenFlowerPreview()
    {
        if(!ImGui.TreeNode("Possible flower colours (demo)")){gardenFlowerPreviewStarted=-1;return;}
        var now=ImGui.GetTime();
        if(gardenFlowerPreviewStarted<0)gardenFlowerPreviewStarted=now;
        if(ImGui.Checkbox("Cycle colours every second",ref gardenFlowerPreviewRunning))gardenFlowerPreviewStarted=now;
        if(gardenFlowerPreviewRunning)gardenFlowerPreviewColour=FlowerColourPreview.At(now-gardenFlowerPreviewStarted);
        ImGui.TextWrapped("Oldrose example · possible rare colours, not a confirmed flower. The normal colour can still be the result. Equal display time does not mean equal odds.");
        var at=ImGui.GetCursorScreenPos();var size=Math.Min(128,ImGui.GetContentRegionAvail().X);
        GardenImage("assets/beds/soil-normal.png",at,new(size));
        GardenImage("assets/flower-colors/oldrose/"+gardenFlowerPreviewColour+".png",at,new(size));
        GardenImage("assets/beds/frame-wood.png",at,new(size));
        ImGui.Dummy(new Vector2(size));
        ImGui.TextUnformatted("Possible: "+gardenFlowerPreviewColour);
        ImGui.TextWrapped("Automatic colour display still needs indoor flowerpot identification and syncing. This demo changes no garden records.");
        ImGui.TreePop();
    }
}
