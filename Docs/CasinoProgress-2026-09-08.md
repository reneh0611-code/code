# Casino integration status — 2026-09-08

The saved city, terrain and material changes were committed as `bccd0708` and pushed to `origin/main`. Fetch found no new main commits. The separate melee-animation branch contains a patch already present in main (`git cherry` reports `-`). Existing scene and project structure were retained.

Casino work started in `Assets/Scripts/Gameplay/Casino/CasinoRules.cs`, with a standalone regression check in `Assets/Editor/VerifyCasinoRules.cs`. No packages or shared player assets were changed.

Validation: both C# files compiled together with PowerShell Add-Type. `VerifyCasinoRules.Run()` passed 6,360 assertions covering 157 roulette bets across all 37 outcomes, blackjack ace/natural/bust/push cases and all 216 slot outcomes. This is rule validation, not Unity runtime or multiplayer validation.

Roulette uses the European wheel with one zero, straight-up bets, splits, streets, corners, six lines, dozens, columns, red/black, odd/even and low/high. Returned payout amounts include the original stake. Blackjack stands on all 17 and pays a natural 3:2. Slots use six equally likely symbols, paying 20x for triple seven, 5x for other triples and 1x for a pair.

Still required: the polished central roulette table in the existing black/gold casino; UI, camera and interaction wiring; server-authoritative wallet integration; blackjack and slot stations; Unity compilation and end-to-end play tests. Earlier staging installers/session scripts are unfinished prototypes and were deliberately not installed. The casino is not yet fully playable.
