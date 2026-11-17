# Workshop 0.5.1.577

The 15:44:24 report shows navigation exploring the lowered workshop floor for about 40 seconds, followed by manual movement. The station object is above that floor. Workshop searches now use a three-dimensional interaction radius, while retaining collision validation, instead of requiring destination floor height. This applies to station, chest and bell approaches. The observed station ID is also recognized directly.

Project selection now selects the CompanyCraftSequence category and type before verifying both sequence and result item in the native list and requesting the craft. Previously the callback ran while Aetherial Wheels was displayed and no confirmation appeared.

Callback layout was checked against Workshoppa/WorkshopPlugin.CraftingLog.cs at b90245cf2801c478030433a5eb1cd70bd4f1d20d and the report. No change to material delivery confirmation scope.

The largest recorded 1.4-second interval occurred during plugin startup, before navigation. The report does not prove the cause of the later perceived freeze. In-game navigation and a full project still require verification.
