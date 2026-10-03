namespace EquinoxCompanion;

// Explicit guided setup: the user opens beds in order; only fresh game interactions supply targets.
// Stage all eight identities so cancelling a partial walk cannot publish a partial manual mapping.
public sealed class GardenBedSyncSession(Actor actor, Address address, int patch, DateTimeOffset startedAt)
{
    // Double-click is reported on press; consume its later release so it cannot restart.
    public static string? ButtonAction(bool pressed,bool doubleClick,bool mouseDown,ref bool suppressRelease)
    {
        if(doubleClick){suppressRelease=true;return "cancel";}
        if(suppressRelease){if(!mouseDown)suppressRelease=false;return null;}
        return pressed?"start":null;
    }
    public static bool NeedsSync(IEnumerable<SharedGardenBed> beds) => Enumerable.Range(1,8).Any(n=>
        !beds.Any(b=>b.Bed==n && b.ObservedAt is not null));
    public static bool IsVisitor(SharedRoster? roster, string name, string homeWorld, string houseId)
    {
        var matches=(roster?.People??[]).SelectMany(p=>p.Characters).Where(c=>
            string.Equals(c.Name,name,StringComparison.OrdinalIgnoreCase)&&string.Equals(c.World,homeWorld,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length==1 && !matches[0].Houses.Any(h=>h.Id==houseId);
    }
    private readonly List<SyncEvent> mappings = [];
    private GardenTargetDetails? anchor;
    public const float MaxDistance = 8;
    public int Patch => patch;
    public int NextBed => mappings.Count + 1;
    public bool Complete => mappings.Count == 8;
    public string Status { get; private set; } = "Open Bed 1 in game. Close its menu, then continue clockwise through Beds 2–8.";
    public IReadOnlyList<SyncEvent> Mappings => mappings;
    public bool CheckDistance(float x, float y, float z)
    {
        if(Complete||anchor is null)return true;
        if(float.IsFinite(x)&&float.IsFinite(y)&&float.IsFinite(z)&&
            Math.Pow(x-anchor.X,2)+Math.Pow(y-anchor.Y,2)+Math.Pow(z-anchor.Z,2)<=MaxDistance*MaxDistance)return true;
        mappings.Clear();
        Status="Target too far from this garden · Setup reset to Bed 1. Return to this patch and inspect Bed 1, or click the sync button to start elsewhere.";
        return false;
    }
    public bool Matches(string contentId, Address? current, int physicalPatch) =>
        actor.ContentId == contentId && address == current && patch == physicalPatch;

    public bool Observe(GardenSnapshot target, DateTimeOffset at, (int Patch, int Bed)? numbered = null)
    {
        if (Complete || patch is < 1 or > 3 || at < startedAt || at < target.ObservedAt ||
            at - target.ObservedAt > TimeSpan.FromSeconds(2) ||
            !Matches(target.Actor.ContentId, target.Address, patch) || target.TargetId is null ||
            target.Address is not { Apartment: false, Workshop: false, Room: 0, Plot: > 0 } ||
            target.TargetDetails is not { DataId: 2003757, EventArgument: {} argument } t ||
            !float.IsFinite(t.X) || !float.IsFinite(t.Y) || !float.IsFinite(t.Z)) return false;
        if (!CheckDistance(t.X,t.Y,t.Z) || mappings.Any(e => e.GardenTarget!.Argument == argument)) return false;
        if (numbered is {} n && (n.Patch != patch || n.Bed != NextBed))
        {
            Status = $"Game menu says Patch {n.Patch}, Bed {n.Bed}. Open Patch {patch}, Bed {NextBed}; this target was not saved.";
            return false;
        }
        anchor??=new(argument,t.X,t.Y,t.Z);
        mappings.Add(new(Guid.NewGuid().ToString("N"), "garden.mapped", at, target.Actor, address,
            patch, NextBed, GardenTarget: new(argument, t.X, t.Y, t.Z)));
        Status = Complete ? "8/8 bed identities recorded. Observed contents are syncing." :
            $"{mappings.Count}/8 recorded · Close this menu and open Bed {NextBed} in game.";
        return true;
    }
}
