# Companion 0.5.1.449

Assigned character switching and lobby login now inspect the exact character entry’s current world before login. When it belongs to another data center, return through data-center selection and resume on the same verified service account. Home-world identity remains the character identity check. If a visiting flag is present but the visiting world cannot be verified, stop with a specific diagnostic rather than retrying the home list. Redirects are recorded in travel exports.

During active automatic login/travel, recognize the English System Configuration Backup age reminder by its title and complete reminder text, and acknowledge its uniquely identified OK button through its registered event. Preserve notification settings. Discovery is bounded to four addons per frame with a cooldown; record detection/dismissal and slice timing. Other backup dialogs and queue prompts are not acknowledged by this handler.

Validation: Release build and automated suites. Regression cases accept varying backup ages and reject queue, restore, upload, incomplete-title and incomplete-reminder prompts. Actual lobby transitions and reminder dismissal still require in-game verification.
