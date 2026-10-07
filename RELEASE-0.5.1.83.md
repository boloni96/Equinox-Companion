# Companion 0.5.1.83

Fixes Helper dialogue capture and recovery after Nananji response-menu timeout. Reads the visible Talk addon's text and speaker, observes setup and line transitions, and flushes the preceding dialogue before a response choice. Captures each transition once even when refresh/finalize/poll overlap.

After a blocked NPC conversation is closed, Helper drops that failed conversation and resumes normal following without Stop/Start. Unknown or different dialogue still does not choose arbitrary responses. Diagnostics now include quest-offer/result and icon-response windows plus captured dialogue, so progress differences can be distinguished from a missing transition.

Journal remains V7.11.83; no Cloudflare deployment required if that version is already installed. Quest acceptance, Nocturne replay mapping, FATE sync and independent pauses remain unchanged. Nananji interaction needs in-game retest.
