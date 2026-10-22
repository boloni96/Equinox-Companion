using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class LoginCancel236Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception("Login Cancel 236: " + reason); }
        var now = DateTimeOffset.Parse("2026-10-22T20:20:00Z");
        var back = new LoginMenuReturn();
        Check(!back.Begin(CharacterLoginMode.CharacterSelect, false, true, now), "a manually opened character list is left alone");
        Check(back.Next(now, false, false, false, true) == LoginMenuReturnAction.Idle, "manual list cancellation sends no native return");
        Check(!back.Begin(CharacterLoginMode.CharacterSelect, true, false, now), "a changed character, pairing or account cannot reuse return ownership");
        foreach (var mode in new[] { CharacterLoginMode.Title, CharacterLoginMode.InGame, CharacterLoginMode.Unavailable })
            Check(!back.Begin(mode, true, true, now), "Cancel never backs out of " + mode);

        Check(back.Begin(CharacterLoginMode.CharacterSelect, true, true, now), "Companion-opened list can return after Cancel");
        Check(!back.Begin(CharacterLoginMode.CharacterSelect, true, true, now.AddSeconds(5)), "a second request cannot extend the deadline");
        Check(back.Next(now, false, false, false, false) == LoginMenuReturnAction.Wait, "waits for native UI readiness");
        Check(back.Next(now.AddMilliseconds(250), false, false, false, true) == LoginMenuReturnAction.ClickReturn, "ready list requests Back, not a login");
        // A refused/disabled button may be retried; an accepted click is sent once.
        Check(back.Next(now.AddMilliseconds(500), false, false, false, true) == LoginMenuReturnAction.ClickReturn, "button not dispatched can be tried when ready");
        back.Clicked();
        Check(back.Next(now.AddSeconds(1), false, false, false, true) == LoginMenuReturnAction.Wait, "does not click repeatedly during return transition");
        Check(back.Next(now.AddSeconds(2), false, false, true, false) == LoginMenuReturnAction.Finished, "arrival at title completes cancellation");
        Check(!back.Active && back.Next(now.AddSeconds(3), false, false, true, true) == LoginMenuReturnAction.Idle, "completion leaves no delayed work");

        back.Begin(CharacterLoginMode.CharacterSelect, true, true, now);
        Check(back.Next(now.AddSeconds(1), true, false, false, true) == LoginMenuReturnAction.Aborted, "a manual login takes priority and is not interrupted");
        back.Begin(CharacterLoginMode.CharacterSelect, true, true, now);
        Check(back.Next(now.AddSeconds(1), false, true, false, true) == LoginMenuReturnAction.Aborted, "an unrelated Lifestream trip is left alone");
        back.Begin(CharacterLoginMode.CharacterSelect, true, true, now);
        Check(back.Next(now.AddSeconds(9), false, false, false, false) == LoginMenuReturnAction.Wait, "temporarily blocked menu can finish becoming ready");
        Check(back.Next(now.AddSeconds(10), false, false, false, true) == LoginMenuReturnAction.TimedOut, "expired return never clicks late even if menu becomes ready");
        Check(!back.Active, "timeout does not leave controls disabled");
        back.Begin(CharacterLoginMode.CharacterSelect, true, true, now);
        back.Stop();
        Check(back.Next(now.AddSeconds(1), false, false, false, true) == LoginMenuReturnAction.Idle, "Stop return prevents a late menu click");
        Check(back.Begin(CharacterLoginMode.CharacterSelect, true, true, now.AddSeconds(20)), "a later independent cancellation can return normally");
        back.Stop();
        Console.WriteLine($"Login Cancel checks passed: {checks}.");
    }
}
