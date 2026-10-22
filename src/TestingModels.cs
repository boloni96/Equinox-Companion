namespace EquinoxCompanion;

public sealed record PlayTest(string Id,int Revision,string Category,string Title,string Steps,string When,bool Archived);
public sealed record TestCatalog(int Schema,string Version,string Title,PlayTest[] Tests);
public sealed record TestReport(string Id,string TestId,int TestRevision,string TesterId,string TesterName,string Status,string Notes,string PluginVersion,string JournalVersion,string WorkerVersion,string Source,string BaseId,long Seq=0,string At="");
public sealed record PendingTestReport(TestReport Report,string Blocked="");
public sealed record TestingLocal(TestCatalog? Catalog=null,long CatalogRevision=0,TestReport[]? Reports=null,PendingTestReport[]? Pending=null,long Cursor=0,bool Complete=false,string ServerVersion="");
public sealed record TestingPage(int Schema,TestCatalog Catalog,long CatalogRevision,TestReport[] Reports,long Cursor,bool More,string ServerVersion);
public sealed record TestingResponse(TestingPage? Page=null,TestReport? Report=null,string Error="",bool Conflict=false);

public static class TestingPolicy
{
 public static string Label(string status)=>status switch {"pass"=>"Pass","fail"=>"Fail","later"=>"Later",_=>"Not tested"};
 public static bool Id(string? value)=>value is {Length:>0 and <=100}&&value.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '_' or ':' or '-');
 public static bool Valid(TestReport? r)=>r is not null&&Id(r.Id)&&Id(r.TestId)&&Id(r.TesterId)&&(r.BaseId==""||Id(r.BaseId))&&r.TestRevision>0&&r.TesterName is {Length:>0 and <=100}&&r.Status is "pass" or "fail" or "later" or "not-tested"&&r.Notes is {Length:<=4000}&&r.PluginVersion is {Length:<=80}&&r.JournalVersion is {Length:<=80}&&r.WorkerVersion is {Length:<=80}&&r.Source is "journal" or "plugin"&&r.Seq>=0&&r.At is {Length:<=80};
 public static bool Valid(TestCatalog? c)=>c is not null&&c.Schema==1&&c.Version is {Length:>0 and <=100}&&c.Title is {Length:<=150}&&c.Tests is {Length:<=500}&&c.Tests.All(t=>t is not null&&Id(t.Id)&&t.Revision>0&&t.Title is {Length:>0 and <=150}&&t.Steps is {Length:<=6000}&&t.Category is {Length:<=100}&&t.When is {Length:<=200})&&c.Tests.Select(t=>t.Id).Distinct().Count()==c.Tests.Length;
 public static TestReport[] Merge(IEnumerable<TestReport> old,IEnumerable<TestReport> incoming)=>old.Concat(incoming).Where(Valid).GroupBy(r=>r.Id).Select(g=>g.MaxBy(r=>r.Seq)!).OrderBy(r=>r.Seq).ToArray();
 public static TestReport[] Current(PlayTest test,IEnumerable<TestReport> reports)=>reports.Where(r=>r.TestId==test.Id&&r.TestRevision==test.Revision).GroupBy(r=>r.TesterId).Select(g=>g.MaxBy(r=>r.Seq)!).ToArray();
 public static string Summary(PlayTest t,IEnumerable<TestReport> reports){var current=Current(t,reports);return current.Any(r=>r.Status=="fail")?"fail":current.Any(r=>r.Status=="pass")?"pass":current.Any(r=>r.Status=="later")?"later":"not-tested";}
 public static bool NeedsRetest(PlayTest t,IEnumerable<TestReport> reports)=>!Current(t,reports).Any()&&reports.Any(r=>r.TestId==t.Id&&r.TestRevision<t.Revision);
 public static TestingLocal Apply(TestingLocal state,TestingPage page)=>state.Cursor==page.Cursor&&state.CatalogRevision==page.CatalogRevision&&state.Catalog?.Version==page.Catalog.Version&&state.Catalog.Title==page.Catalog.Title&&state.Catalog.Tests.SequenceEqual(page.Catalog.Tests)&&state.ServerVersion==page.ServerVersion&&state.Complete==!page.More&&page.Reports.Length==0?state:state with {Catalog=page.Catalog,CatalogRevision=page.CatalogRevision,Reports=Merge(state.Reports??[],page.Reports),Cursor=page.Cursor,Complete=!page.More,ServerVersion=page.ServerVersion};
 public static TestingLocal Acknowledge(TestingLocal state,TestReport report)=>state with {Reports=Merge(state.Reports??[],[report]),Pending=(state.Pending??[]).Where(p=>p.Report.Id!=report.Id).ToArray()};
}
