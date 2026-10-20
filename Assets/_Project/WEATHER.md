# Weather Ver.2.0 integration

- GameMasterData references Settings/Weather/WeatherConfig.asset. Default speed remains 5 real seconds per game minute.
- Tick updates weather every minute; HourTick updates the existing environment bookkeeping and bee lifecycle once per hour.
- Baseline is stored as 525600 x 8 doubles inside the native WeatherBaseline.asset (ScriptableObject). No TextAsset, CSV or binary input is loaded at runtime. Source.json records the verified workbook identity and checksum.
- Calculation uses double precision and dedicated, stateful weather/wind random streams.
- Current sample includes cloud fraction, wind m/s, vapor hPa, precipitation mm per minute interval, solar MJ/m2 per minute interval, temperature C, RH %, snow cm and soil storage mm.
- EnvironmentState exposes precipitation intensity (minute amount x 60) and display brightness only. Daylight maps solar power to 0..1 at 1000 W/m2; use SolarEnergyMJPerM2 for physical calculations.
- Initialization evaluates the baseline at the configured starting minute. Starting at a later date creates a fresh weather state at that date; it does not replay elapsed years or perform the 50-year validation warmup.
- Annual wrapping affects baseline lookup only. Weather reservoirs, energy, snow, soil and RNG states continue.
- Capture/Restore supports exact in-memory continuation with matching parameters and baseline. Disk save/load is not implemented by this change.
- Parameter field keys match Excel cells; comments identify the original parameter where available.
- This ports the calibrated model as-is, including existing wind midnight transitions and rare long snow cover. No recalibration is applied.
- No cloud/rain particles or sky rendering are added. Current weather is connected to the existing observation UI.
