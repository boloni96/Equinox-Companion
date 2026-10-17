using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class UpgradeRetry177Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string label){if(!ok)throw new Exception("177: "+label);}
  Check(new[]{0,1,2,3,100}.Select(UpgradeRetryPolicy.DelaySeconds).SequenceEqual(new[]{30,60,120,300,300}),"bounded retry delays");
  foreach(var status in new[]{408,429,500,502,503,504})Check(UpgradeRetryPolicy.IsTransient(new HttpRequestException("unavailable",null,(HttpStatusCode)status)),"retry "+status);
  foreach(var status in new[]{400,401,403,404})Check(!UpgradeRetryPolicy.IsTransient(new HttpRequestException("correct setup",null,(HttpStatusCode)status)),"stop "+status);
  Check(UpgradeRetryPolicy.IsTransient(new HttpRequestException("network")),"network retry");
  Check(UpgradeRetryPolicy.IsTransient(new TaskCanceledException("timeout")),"timeout retry");
  Check(!UpgradeRetryPolicy.IsTransient(new InvalidOperationException("deploy Journal")),"setup errors stop");
  Console.WriteLine("PASS 177 bounded housing retry schedule and transient versus setup failures");
 }
}
