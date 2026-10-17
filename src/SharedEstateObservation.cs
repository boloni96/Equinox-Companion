namespace EquinoxCompanion;

public sealed record SharedEstateObservation(string Evidence, Address[] Houses);
// A missing housing snapshot is not a negative membership observation.
public sealed class SharedEstateObservationGate
{
 private string stamp="";private DateTimeOffset since,last;
 public void Reset(){stamp="";since=last=default;}
 public bool Ready(string actor,string value,bool loaded,DateTimeOffset now)
 {
  if(!loaded){stamp="";since=default;return false;}
  if(last!=default&&now-last>TimeSpan.FromSeconds(20))stamp="";
  last=now;var next=actor+":"+value;
  if(next!=stamp){stamp=next;since=now;return false;}
  return now-since>=TimeSpan.FromSeconds(30);
 }
}
