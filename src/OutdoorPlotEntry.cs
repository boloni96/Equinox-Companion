using System.Numerics;
namespace EquinoxCompanion;
// The same bounded entry used after private/FC teleport, without door actions.
internal sealed class OutdoorPlotEntry(Vector3 direction)
{
    public Vector3 Direction { get; }=Vector3.Normalize(new(direction.X,0,direction.Z));
    public Vector3? Goal;
    public int Steps;
    private Vector3? confirmationGoal;
    public bool FinishingEntry=>confirmationGoal.HasValue;
    // Estate identity can become available on the street-side edge of a gate.
    // Finish the current entry segment before handing movement to the garden.
    public bool Confirm(Vector3 position,bool matchingEstate){
        if(!matchingEstate)return false;
        confirmationGoal??=Goal??Next(position);
        var goal=confirmationGoal.Value;
        var offset=position-goal;var along=Vector3.Dot(new(offset.X,0,offset.Z),Direction);
        var sideways=new Vector2(offset.X-Direction.X*along,offset.Z-Direction.Z*along).Length();
        return along>=-.35f&&along<=1&&sideways<=.75f&&MathF.Abs(offset.Y)<=.7f;
    }
    public Vector3 Next(Vector3 position){
        if(Steps>=6)throw new InvalidOperationException("Plot entry did not reach the requested estate within six short steps.");
        Steps++;return (Goal=position+Direction*3).Value;
    }
}
