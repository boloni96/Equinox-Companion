namespace EquinoxCompanion;
internal sealed class QuestMovementPause
{
    private DateTimeOffset until;
    public bool Observe(DateTimeOffset now,bool movement)
    {
        if(movement)until=now.AddMilliseconds(500);
        return now<until;
    }
}
