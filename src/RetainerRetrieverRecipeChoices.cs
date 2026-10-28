using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong retrievalPreviewOwner;
    private uint retrievalRecipeChoice;
    private readonly Dictionary<uint,uint> retrievalRecipeOverrides=[];
    private IReadOnlyDictionary<uint,RetrievalRecipe[]> RetrievalRecipes()
    {
        var catalog=retrievalCatalog!.Recipes;
        if(retrievalRecipeOverrides.Count==0)return catalog;
        var copy=new Dictionary<uint,RetrievalRecipe[]>(catalog);
        foreach(var (item,id) in retrievalRecipeOverrides)if(catalog.TryGetValue(item,out var choices)&&choices.FirstOrDefault(c=>c.Id==id) is {} selected)copy[item]=[selected];
        return copy;
    }
    private void DrawRetrievalRecipeChoice()
    {
        if(retrievalRecipeChoice==0||retrievalCatalog==null||!retrievalCatalog.Recipes.TryGetValue(retrievalRecipeChoice,out var choices))return;
        ImGui.TextWrapped("Recipe needed: "+retrievalCatalog.Names.GetValueOrDefault(retrievalRecipeChoice));
        if(!ImGui.BeginCombo("##ResolveRecipe","Choose recipe…"))return;
        foreach(var r in choices)
        {
            if(ImGui.Selectable(r.Job+" · recipe "+r.Id+" · yield "+r.Yield))
            {
                retrievalRecipeOverrides[retrievalRecipeChoice]=r.Id;
                retrievalItems=retrievalItems.Select(e=>e.ItemId==retrievalRecipeChoice?e with{RecipeId=r.Id}:e).ToArray();
                retrievalRecipeChoice=0;retrievalPreviewDirty=true;retrievalError="";
            }
            if(ImGui.IsItemHovered())ImGui.SetTooltip(string.Join("\n",r.Ingredients.Select(i=>i.Quantity+" × "+i.Name)));
        }
        ImGui.EndCombo();
    }
}
