namespace EquinoxCompanion;

// Older Journal versions flattened reward-source HTML into the shared task label.
// Only the rendered label is shortened; the synced record remains untouched.
internal static class AdventureTaskPresentation
{
 internal static (string Label,string Details) Split(string label,string? kind)
 {
  var clean=label;
  var details="";
  if(kind=="reward")
  {
   var checkedAt=label.IndexOf("Source information checked ",StringComparison.Ordinal);
   var disclaimer=label.IndexOf("Listed sources may require quests, currency or access.",StringComparison.Ordinal);
   if(checkedAt>=0||disclaimer>=0)
   {
    var end=checkedAt>=0?checkedAt:disclaimer;
    foreach(var marker in new[]{"No alternative verified","Other in-game sources listed","Market Board","Reclaim conditions apply","Paid-store source listed","Item mapping pending"})
    {
     var index=label.LastIndexOf(marker,end,StringComparison.Ordinal);
     if(index>0){end=index;break;}
    }
    if(end>0){clean=label[..end].TrimEnd();details=label[end..];}
   }
  }
  var suffixes=kind switch {
   "event"=>new[]{" · Complete event activity"," · Activity completed"},
   "reward"=>new[]{" · Collect reward"," · Obtained"},
   _=>Array.Empty<string>()
  };
  foreach(var suffix in suffixes)
   if(clean.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)&&clean.Length>suffix.Length)
   {clean=clean[..^suffix.Length];break;}
  return(clean,details);
 }
}
