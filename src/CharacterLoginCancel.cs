namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly LoginMenuReturn loginMenuReturn = new();
    private bool characterSwitchReturnOnCancel;
    private DateTimeOffset nextLoginMenuReturn;

    private static unsafe bool LoginMenuUnlocked()
    {
        var lobby = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentLobby.Instance();
        return lobby != null && !lobby->TemporaryLocked;
    }

    private void CancelCharacterLoginConfirmation(CharacterLoginMode mode, bool consentCurrent)
    {
        var returning = !Player.IsLoaded && !Client.IsLoggedIn
            && loginMenuReturn.Begin(mode, characterSwitchReturnOnCancel, consentCurrent, DateTimeOffset.UtcNow);
        characterSwitchReturnOnCancel = false;
        characterSwitchConfirmTarget = "";
        queuedLoginConfirmation = "";
        nextLoginMenuReturn = default;
        characterTravelStatus = returning ? "Cancelled · returning to the main menu…" : "Character login cancelled.";
    }

    private void UpdateCancelledLoginReturn(DateTimeOffset now)
    {
        if (!loginMenuReturn.Active || now < nextLoginMenuReturn) return;
        nextLoginMenuReturn = now.AddMilliseconds(250);
        try
        {
            var busy = LifestreamBusy() || titleLoginProbe.Active || shoppingTravel is not null || bringTrip is not null || followSession.Armed;
            var mode = ReadCharacterLoginMode(true);
            // Use the existing ready-list gate. Never dismiss login, connection,
            // world-selection or confirmation dialogs on behalf of Cancel.
            var action = loginMenuReturn.Next(now, Client.IsLoggedIn || Player.IsLoaded, busy,
                mode == CharacterLoginMode.Title,
                mode == CharacterLoginMode.CharacterSelect && LoginMenuUnlocked()
                    && !VisibleFollowAddon("SelectString") && !VisibleFollowAddon("TitleConnect"));
            switch (action)
            {
                case LoginMenuReturnAction.ClickReturn:
                    if (ClickLoginMenuButton("_CharaSelectReturn", 4)) loginMenuReturn.Clicked();
                    break;
                case LoginMenuReturnAction.Finished:
                    characterTravelStatus = "Cancelled · back at the main menu.";
                    break;
                case LoginMenuReturnAction.Aborted:
                    characterTravelStatus = "Return cancelled because another login or trip started.";
                    break;
                case LoginMenuReturnAction.TimedOut:
                    characterTravelStatus = "Login cancelled. The game did not return to the main menu; use its Back button.";
                    break;
            }
        }
        catch
        {
            loginMenuReturn.Stop();
            characterTravelStatus = "Login cancelled. Use the game's Back button to return to the main menu.";
        }
    }
}
