using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private static SeString EquinoxChat(SeString message)
 {
  var builder=new SeStringBuilder().AddUiForeground(543).AddText("[Equinox]").AddUiForegroundOff().AddText(" ");
  foreach(var payload in message.Payloads){
   if(payload is TextPayload text)builder.AddText(text.Text.Replace("[Equinox] ",""));
   else builder.Add(payload);
  }
  return builder.Build();
 }
 private void PrintEquinox(string message)=>Chat.Print(EquinoxChat(new SeStringBuilder().AddText(message).Build()));
 private void PrintEquinox(SeString message)=>Chat.Print(EquinoxChat(message));
 private void PrintEquinoxError(string message)=>Chat.PrintError(EquinoxChat(new SeStringBuilder().AddText(message).Build()));
}
