using System.Text.Json.Serialization;

namespace DEThermo.Cli;

internal sealed class Scenario
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("mass_kg")]
    public double MassKg { get; set; }
    [JsonPropertyName("initial_temp_c")]
    public double InitialTempC { get; set; }
    [JsonPropertyName("ambient_temp_c")]
    public double AmbientTempC { get; set; }
    [JsonPropertyName("area_m2")]
    public double AreaM2 { get; set; }
    [JsonPropertyName("htc_w_m2k")]
    public double HtcWm2K { get; set; }
    [JsonPropertyName("cp_liquid_j_kgk")]
    public double CpLiquidJkgK { get; set; } = 4186.0;
    [JsonPropertyName("cp_ice_j_kgk")]
    public double CpIceJkgK { get; set; } = 2100.0;
    [JsonPropertyName("latent_heat_j_kg")]
    public double LatentHeatJkg { get; set; } = 333_700.0;
}

internal sealed class Milestones
{
    [JsonPropertyName("reaches_zero_s")]
    public double? ReachesZeroS { get; set; }
    [JsonPropertyName("freeze_complete_s")]
    public double? FreezeCompleteS { get; set; }
    [JsonPropertyName("reaches_target_s")]
    public double? ReachesTargetS { get; set; }
    [JsonPropertyName("reason_unreachable")]
    public string? ReasonUnreachable { get; set; }
}

internal sealed class TrajectoryPoint
{
    [JsonPropertyName("t_s")]
    public double TS { get; set; }
    [JsonPropertyName("temp_c")]
    public double TempC { get; set; }
}

internal sealed class SimulationOutput
{
    [JsonPropertyName("scenario")]
    public Scenario Scenario { get; set; } = new();
    [JsonPropertyName("target_temp_c")]
    public double TargetTempC { get; set; }
    [JsonPropertyName("milestones")]
    public Milestones Milestones { get; set; } = new();
    [JsonPropertyName("points")]
    public List<TrajectoryPoint> Points { get; set; } = [];
}

internal sealed class BatchInput
{
    [JsonPropertyName("target_temp_c")]
    public double? TargetTempC { get; set; }
    [JsonPropertyName("duration_s")]
    public double? DurationS { get; set; }
    [JsonPropertyName("step_s")]
    public double? StepS { get; set; }
    [JsonPropertyName("scenarios")]
    public List<Scenario> Scenarios { get; set; } = [];
}

internal sealed class BatchRecord
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("reaches_zero_s")]
    public double? ReachesZeroS { get; set; }
    [JsonPropertyName("freeze_complete_s")]
    public double? FreezeCompleteS { get; set; }
    [JsonPropertyName("reaches_target_s")]
    public double? ReachesTargetS { get; set; }
    [JsonPropertyName("reason_unreachable")]
    public string? ReasonUnreachable { get; set; }
}

internal sealed class BatchOutput
{
    [JsonPropertyName("input")]
    public string Input { get; set; } = "";
    [JsonPropertyName("target_temp_c")]
    public double TargetTempC { get; set; }
    [JsonPropertyName("duration_s")]
    public double DurationS { get; set; }
    [JsonPropertyName("step_s")]
    public double StepS { get; set; }
    [JsonPropertyName("records")]
    public List<BatchRecord> Records { get; set; } = [];
}

internal sealed class OptimizeResult
{
    [JsonPropertyName("freeze_deadline_s")]
    public double FreezeDeadlineS { get; set; }
    [JsonPropertyName("min_htc_w_m2k")]
    public double? MinHtcWm2K { get; set; }
    [JsonPropertyName("achieved_freeze_s")]
    public double? AchievedFreezeS { get; set; }
    [JsonPropertyName("feasible")]
    public bool Feasible { get; set; }
}

internal sealed class MonteCarloConfig
{
    [JsonPropertyName("samples")]
    public int Samples { get; set; } = 2000;
    [JsonPropertyName("seed")]
    public int Seed { get; set; } = 42;
    [JsonPropertyName("mass_cv")]
    public double MassCv { get; set; } = 0.05;
    [JsonPropertyName("htc_cv")]
    public double HtcCv { get; set; } = 0.10;
    [JsonPropertyName("area_cv")]
    public double AreaCv { get; set; } = 0.05;
    [JsonPropertyName("initial_temp_std_c")]
    public double InitialTempStdC { get; set; } = 1.0;
    [JsonPropertyName("ambient_temp_std_c")]
    public double AmbientTempStdC { get; set; } = 1.0;
}

internal sealed class MonteCarloSummary
{
    [JsonPropertyName("samples")]
    public int Samples { get; set; }
    [JsonPropertyName("deadline_s")]
    public double DeadlineS { get; set; }
    [JsonPropertyName("freeze_by_deadline_probability")]
    public double FreezeByDeadlineProbability { get; set; }
    [JsonPropertyName("target_by_deadline_probability")]
    public double TargetByDeadlineProbability { get; set; }
    [JsonPropertyName("freeze_time_mean_s")]
    public double? FreezeTimeMeanS { get; set; }
    [JsonPropertyName("freeze_time_std_s")]
    public double? FreezeTimeStdS { get; set; }
    [JsonPropertyName("target_time_mean_s")]
    public double? TargetTimeMeanS { get; set; }
    [JsonPropertyName("target_time_std_s")]
    public double? TargetTimeStdS { get; set; }
}
