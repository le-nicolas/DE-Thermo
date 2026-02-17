# Published Results Snapshot

Date: 2026-02-17
Engine: `DEThermo.Cli` (.NET 9, Release)

## Batch summary (`scenarios/sample_scenarios.json`)

- Scenarios processed: 4
- Freeze-valid: 4
- Target-valid (`-12 C`): 4
- Fastest freeze: `Espresso Shot` at `13058.0 s`

## Optimization snapshot

For:

- `mass_kg = 0.35`
- `initial_temp_c = 90`
- `ambient_temp_c = -18`
- `area_m2 = 0.03`
- `freeze_deadline_s = 21600`

Result:

- `min_htc_w_m2k = 14.064379489933849`
- `achieved_freeze_s = 21600`
- `feasible = true`

## Monte Carlo snapshot (5000 samples, 12h deadline)

For the ceramic mug baseline:

- `freeze_by_deadline_probability = 0.8236`
- `target_by_deadline_probability = 0.6188`
- `freeze_time_mean_s = 38566.061488275234`
- `target_time_mean_s = 42015.61392407661`
