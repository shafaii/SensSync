# Target conversion models

These are horizontal mouse hipfire estimates, not verified live-game matching. Conversion does not read or write target-game settings. ADS, scopes, vehicles, acceleration, aim smoothing, FOV perception and hero-specific overrides are not matched.

The source is converted to physical cm/360 internally using the saved source sensitivity and its game-specific turn-rate model. At the entered DPI, the required degrees per mouse count are `914.4 / DPI / cmPer360`.

## Valorant

Retains the original M1 candidate yaw of 0.07 degrees per count per sensitivity unit. This intentionally preserves existing results and clipboard precision. KovaaK's currently lists 0.06996 instead; this small discrepancy has not been resolved by physical game testing. The app does not silently change the original model.

## Counter-Strike 2

Target yaw is the default 0.022. When the saved source has a custom yaw, the converter preserves its physical distance and computes the target value assuming default target yaw. It does not read or apply a target yaw setting. Source m_yaw provenance remains visible in Details.

## Marvel Rivals

Candidate yaw: 0.0175. Output is the horizontal mouse sensitivity setting. The source reader uses the saved global horizontal sensitivity or an explicitly selected differing hero override. Automatic application is not implemented. Test a measured 360-degree turn in game before relying on the match.

## Battlefield 6 (experimental)

The target is the infantry hipfire **menu slider**, not GstInput.MouseSensitivity in a configuration file. The published KovaaK's model is affine:

`degreesPerCount = menuSensitivity * 0.0017362342283 + 0.002314981532`

SensSync independently implements the inverse: subtract the base rate from the desired degrees per count, then divide by the slope. It rejects output outside 0–100 instead of clamping or showing an impossible value. Zero is a valid menu value with non-zero turn speed. The game may round decimal inputs, so the computed result is a desired match, not proof of the final applied turn rate.

This model has not been validated in a current BF6 game session. Community observations are not fully consistent across builds; in particular, Jotunn's beta-based guide gives a physical-distance example that does not agree exactly with the current KovaaK's model. Therefore the UI explicitly labels BF6 experimental. Do not assume old Battlefield game scales are interchangeable.

## Sources inspected on 2026-10-16

- [KovaaK's own converter](https://kovaaks.com/kovaaks/sens-converter), including its [public converter asset](https://kovaaks.com/_next/static/chunks/app/(crawlable)/kovaaks/sens-converter/page-500d97ff45a869fb.js). Its game-scale entries supplied the Marvel Rivals coefficient and BF6 slope/offset above. No application source was copied; only numeric model facts were used.
- [KovaaK's explanation of yaw and conversion](https://www.kovaak.com/sensitivity-matcher/).
- [Jotunn's first-hand BF6 Labs/Open Beta observations](https://gist.github.com/Jotunn/a574f25d18376f53bd1fb0ff8e4b9e04), which distinguish the 0–100 menu scale from configuration-file values. These observations do not qualify the current game build.

Automated tests verify the calculations, supported range, source/target independence, clipboard values, invalidation, and UI behavior. They do not establish physical accuracy in the games. New games require their own researched and validated models; there is no universal sensitivity unit.

## Saved-source mapping

Rivals and Valorant source readers use the same candidate linear coefficients as their output models. CS2 uses actual saved m_yaw. BF6 source config values are divided by 0.00075 to obtain the menu scale before using the affine model. This mapping was checked against a local saved profile and Jotunn's guide; live-build physical matching is still experimental. All four reader formats were inspected locally and tested with synthetic fixtures. No personal user configurations are included.
