using System.Numerics;
namespace EquinoxCompanion;
// The same bounded entry used after private/FC teleport, without door actions.
internal sealed class OutdoorPlotEntry(Vector3 direction)
{
    public Vector3 Direction { get; }=Vector3.Normalize(new(direction.X,0,direction.Z));
    public Vector3? Goal;
    public int Steps;
    public Vector3 Next(Vector3 position){
        if(Steps>=6)throw new InvalidOperationException("Plot entry did not reach the requested estate within six short steps.");
        Steps++;return (Goal=position+Direction*3).Value;
    }
}
