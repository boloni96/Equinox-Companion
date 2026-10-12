# Companion 0.5.1.133

Official game maintenance reminders appear no earlier than five minutes after each character login. A character switch or relog starts a fresh delay. Reminders show the scheduled date/time in the computer's local timezone, in AM/PM format, a countdown, the estimated end and a clickable official Lodestone notice.

The plugin reads the official maintenance category directly and matches All Worlds or the character's home world/data center. Website, Mog Station and Companion-app notices are excluded. After the initial delay, it checks every 15 minutes and announces a new or revised schedule once per login. Failed checks retry after two minutes with one failure notice. Timers never imply servers are back online after the announced end; unknown schedules link to the notice without inventing dates.

The notification can be disabled in chat-message settings. Network work runs outside the game thread. No Journal update or pairing is required for this reminder. Local computer time is used because the shared Person records do not contain a timezone.

Validation: timezone/date parsing and notification session regression tests, release compilation. Live login/chat-link behavior needs in-game validation.
