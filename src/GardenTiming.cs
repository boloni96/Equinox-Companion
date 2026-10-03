namespace EquinoxCompanion;
public static class GardenTiming
{
    // Planting starts growth/neglect clocks, but does not confirm a Tend action.
    public static bool FirstTendDue(DateTimeOffset? planted,DateTimeOffset? watered,DateTimeOffset now) => planted is {} start && start<=now && !(watered is {} tend && tend>start && tend<=now);
    // An estimated maturity after the neglect deadline cannot revive a potentially dead plant.
    // Actual game confirmation always overrides both estimates.
    public static bool DeathRisk(bool ready,DateTimeOffset? death,DateTimeOffset? maturity,DateTimeOffset now)=>
        !ready&&death is {} deadline&&deadline<=now&&!(maturity is {} harvest&&harvest<deadline);
}
