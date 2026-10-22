namespace EquinoxCompanion;

internal enum LoginMenuReturnAction { Idle, Wait, ClickReturn, Finished, Aborted, TimedOut }

// Only undo the native list opened by Companion for this confirmation. This
// never starts a character login, searches for a target, or owns Lifestream.
internal sealed class LoginMenuReturn
{
    public bool Active { get; private set; }
    private bool clicked;
    private DateTimeOffset deadline;

    public bool Begin(CharacterLoginMode mode, bool openedFromTitle, bool consentCurrent, DateTimeOffset now)
    {
        if (Active || mode != CharacterLoginMode.CharacterSelect || !openedFromTitle || !consentCurrent) return false;
        Active = true; clicked = false; deadline = now.AddSeconds(10);
        return true;
    }

    public LoginMenuReturnAction Next(DateTimeOffset now, bool loggedIn, bool otherTravel, bool atTitle, bool ready)
    {
        if (!Active) return LoginMenuReturnAction.Idle;
        if (loggedIn || otherTravel) { Stop(); return LoginMenuReturnAction.Aborted; }
        if (atTitle) { Stop(); return LoginMenuReturnAction.Finished; }
        if (now >= deadline) { Stop(); return LoginMenuReturnAction.TimedOut; }
        return !clicked && ready ? LoginMenuReturnAction.ClickReturn : LoginMenuReturnAction.Wait;
    }

    public void Clicked() { if (Active) clicked = true; }
    public void Stop() { Active = false; clicked = false; deadline = default; }
}
