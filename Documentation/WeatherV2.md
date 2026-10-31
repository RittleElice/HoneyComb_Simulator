# Weather Ver.2.0 runtime

Weather is generated every game minute. The top-left Weather button opens a read-only current-weather window shared by Apiary and Observation. It refreshes each minute and closes with Close or the same Weather button. No weather visual effect, bee/forage behavior, or hive-environment coupling is enabled.

## Timeline and integration

`GameBootstrap` creates a `WeatherSimulator` with `DefaultSeed = 20261031` for a new game. To select another seed in code, use `WeatherAssetLoader.Create(ulong seed)` and pass it to `new GameWorld(weather)`.

Minute 0 is computed once during construction and corresponds to original workbook row 2. `GameTimeRuntime.BeginWorld(GameWorld)` registers that world's weather instance. Each scheduled next minute computes weather before the clock commits. Live play retains the existing five real seconds per game minute. Time jumps and UTC offline catch-up use the same scheduler and never skip weather steps.

Read `world.Weather.Current` for an immutable snapshot of eight outputs and its minute. Per-minute precipitation and solar energy are interval amounts, not hourly rates. Wind, water and solar physical units remain inferred as described in the extraction specification. Temperature is Celsius, vapor pressure hPa, relative humidity percent, and cloud fraction 0–1. Soil water is storage, not a percentage.

## Model and baseline

`WeatherEquations.cs` contains compiled expressions traced to `FormulaDefinitions.csv` pattern IDs. All 58 calculation columns and eight outputs are evaluated in dependency order with binary64 arithmetic. Parameter expressions regenerate stochastic initial conditions from the chosen seed. Saved Excel random results are not used as universal initial values.

The native `Resources/Weather/WeatherBaselineV2.asset` references `WeatherBaselineV2Data.bytes`. The binary is a 16-byte header (`HCBWV200`, little-endian row count 525600 and column count 8) followed by 4,204,800 little-endian doubles, retaining the extracted baseline without resampling. It is verified by SHA-256 before use and loaded into an immutable array. Runtime memory for that array is about 33.64 MB, in addition to Unity's imported TextAsset storage. There is no runtime Excel dependency.

The source supplies one non-leap year only. This implementation explicitly repeats the baseline using minute modulo 525600, while stochastic, thermal, water and snow states continue across years without reinitialization. This is a runtime extension, not a claim about an absent multi-year workbook formula.

## Randomness and reproducibility

Versioned PCG32 streams are independently seeded with SplitMix64 mixing for cloud slow/fast, wind day/minute, rain supply/intensity, initial smoothed rain, rain decision, and vapor variation. Uniforms are `(uint32 + 0.5) / 2^32`, excluding both endpoints. Acklam's inverse-normal approximation uses one uniform per Gaussian and has no spare normal cache. Conditional branches consume random values only when executed. `UnityEngine.Random`, `System.Random`, system time and frame timing are not used by the model.

The same seed, model/baseline versions, and runtime arithmetic produce identical state. Save/load preserves every RNG stream. Exact historical Excel random sequences cannot be reconstructed. Bit-identical transcendentals across different CPUs, Mono and IL2CPP are not guaranteed by `System.Math`; cross-platform device equivalence needs separate validation.

## Saves and migration

World save schema 2 appends weather state; schema 1 remains readable. Weather state records its schema, equation identity, baseline hash, seed, last calculated minute, all current column values, chronological four-sample histories for both five-minute rain filters, and nine RNG states. The standalone weather payload is 941 bytes after both histories fill. Parameters are rebuilt using a separate initialization generator and never advance restored RNG streams. Scratch buffers are derived.

Decode is safe on the existing background save/load worker and does not access Unity APIs. The main-thread load preparation binds the immutable baseline. Legacy saves without weather are deterministically replayed from minute zero to the saved clock using the migration seed 20261031, yielding every few milliseconds. Other world data and prior save generations are retained. Long legacy timelines take proportionally longer on their first migration.

Only the known registered weather participant is permitted through the existing save boundary. Unknown participants still block saving. Weather/clock mismatch, unsupported model versions, truncated state and invalid numeric state fail closed. The two-generation checksummed save store remains in place. Do not reset weather on scene changes or attach a second weather participant.

## Validation entry points

`WeatherUnityTests.RunBatch` checks native asset loading, world serialization, deterministic continuation, schema-1 migration and previous-generation recovery, then runs existing world-save and clock regression tests.

`WeatherUnityTests.BeginPlayBatch` exercises the actual Bootstrap scene, a live minute, a two-day jump, save, Bootstrap restart, Continue and UTC offline catch-up. Set `HONEYCOMB_SAVE_TEST_ROOT` to an isolated directory and `HONEYCOMB_WEATHER_REPORT_ROOT` for reports; never point tests at player saves.

The separate extraction-based harness compares 8,680 sampled minute rows / 703,080 column values with cached Excel calculations after reconstructing stochastic innovations and rain decisions from those caches, including the first four minutes and their partially filled windows. This validates deterministic equations, not Excel RNG replay. It also compares two years of uninterrupted generation against scheduler/save/resume execution at 15 boundaries, including midnight and year transitions.

`WeatherUnityTests.BeginColdBatch` runs in a separate Unity process against the previous Play test save and compares resumed weather with an uninterrupted seeded run. It uses the same isolated environment settings.
