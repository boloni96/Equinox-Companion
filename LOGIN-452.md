# Companion 0.5.1.452

A visiting-character redirect reached Materia but remained on Sephirot. The user manually selected Sophia and login still did not proceed. The redirect gate and click guard incorrectly used LobbyData.CurrentWorldId to infer the displayed data center, whereas the existing world selector tracks AgentLobby.WorldId. Both now use the displayed world.

The target-world search now receives the current visiting-world request rather than the character's home world and prioritizes that explicit destination over cached world hints. A verified current-world entry can refresh the request. Login is requested only on the intended displayed world; CID/name/home-world/service-account confirmation remains required. Export diagnostics now include displayedWorld, lobbyCurrentWorld, requestedWorld and pending redirect data center to distinguish these states.

Validation: release build and gate regressions locally; all suites in CI. Native lobby transitions require in-game validation. This release does not address the separately reported Market timing spike.
