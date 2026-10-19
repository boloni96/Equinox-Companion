namespace EquinoxCompanion;

// Bounded requests on the framework thread. No windows are opened, and no
// identity is accepted until ObserveDetails finds our content ID in the reply.
public sealed class OwnedCompanyRefresh
{
    private string character = "";
    private DateTimeOffset next, membersDue;
    public int Tick(string contentId, DateTimeOffset now, bool needed, bool safe)
    {
        if (character != contentId) { character = contentId; next = membersDue = default; }
        if (!needed) { membersDue = default; return 0; }
        if (!safe) { membersDue = default; return 0; }
        if (membersDue != default)
        {
            if (now < membersDue) return 0;
            membersDue = default;
            return 2;
        }
        if (now < next) return 0;
        next = now.AddMinutes(1);
        membersDue = now.AddSeconds(5);
        return 1;
    }
}
