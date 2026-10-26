namespace EquinoxCompanion;

// Only a typed game log event can record completion. Player chat and rendered
// message text are never consulted. A scope covers one explicitly locked offer.
internal sealed class GilTradeCompletionCapture
{
    private sealed record Scope(ulong Character, uint Partner, uint Amount, DateTimeOffset ArmedAt);
    private readonly object sync = new();
    private Scope? scope;
    private DateTimeOffset? completedAt;

    internal void Arm(ulong character, uint partner, uint amount, DateTimeOffset now)
    {
        lock (sync)
        {
            scope = character != 0 && partner != 0 && amount is > 0 and <= GilTradeRun.TradeLimit
                ? new(character, partner, amount, now) : null;
            completedAt = null;
        }
    }
    internal void Clear() { lock (sync) { scope = null; completedAt = null; } }
    internal void Observe(uint logMessageId, DateTimeOffset now)
    {
        if (logMessageId is not (36 or 38 or 39 or 40 or 42 or 43 or 46 or 47 or 54)) return;
        lock (sync)
        {
            if (scope == null || now < scope.ArmedAt) return;
            // LogMessage 38 = Trade complete. The other IDs cancel/fail a trade.
            if (logMessageId == 38) completedAt ??= now;
            else if (completedAt == null) scope = null;
        }
    }
    internal bool Completed(ulong character, uint partner, uint amount, DateTimeOffset now)
    {
        lock (sync)
            return scope is {} s && completedAt is {} at && s.Character == character &&
                s.Partner == partner && s.Amount == amount && now >= at && now - at <= TimeSpan.FromSeconds(12);
    }
}
