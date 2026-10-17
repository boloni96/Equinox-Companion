using System.Net;
using System.Net.Http;
namespace EquinoxCompanion;
public static class UpgradeRetryPolicy
{
 public static int DelaySeconds(int attempt) => attempt switch { <=0=>30,1=>60,2=>120,_=>300 };
 public static bool IsTransient(Exception? error) => error is OperationCanceledException || error is HttpRequestException http && (http.StatusCode is null || http.StatusCode==HttpStatusCode.RequestTimeout || (int)http.StatusCode==429 || (int)http.StatusCode>=500);
}
