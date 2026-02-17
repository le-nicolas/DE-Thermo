namespace DEThermo.Gui;

internal sealed class ScenarioInput
{
    public string Name { get; set; } = "";
    public double MassKg { get; set; }
    public double InitialTempC { get; set; }
    public double AmbientTempC { get; set; }
    public double AreaM2 { get; set; }
    public double HtcWm2K { get; set; }
    public double CpLiquidJkgK { get; set; } = 4186.0;
    public double CpIceJkgK { get; set; } = 2100.0;
    public double LatentHeatJkg { get; set; } = 333_700.0;
}

internal sealed class Milestones
{
    public double? ReachesZeroS { get; set; }
    public double? FreezeCompleteS { get; set; }
    public double? ReachesTargetS { get; set; }
    public string? ReasonUnreachable { get; set; }
}

internal sealed class TrajectoryPoint
{
    public double TS { get; set; }
    public double TempC { get; set; }
}

internal sealed class SimulationOutput
{
    public ScenarioInput Scenario { get; set; } = new();
    public double TargetTempC { get; set; }
    public Milestones Milestones { get; set; } = new();
    public List<TrajectoryPoint> Points { get; set; } = [];
}

internal static class ThermoEngine
{
    private const double Eps = 1e-9;

    public static SimulationOutput SimulateTrajectory(ScenarioInput scenario, double targetTempC, double durationS, double stepS)
    {
        ValidateScenario(scenario);
        if (durationS <= 0.0)
        {
            throw new ArgumentException("Duration must be > 0.");
        }
        if (stepS <= 0.0)
        {
            throw new ArgumentException("Step must be > 0.");
        }

        var milestones = ComputeMilestones(scenario, targetTempC);
        var points = new List<TrajectoryPoint>();
        var steps = (int)Math.Ceiling(durationS / stepS);
        for (var i = 0; i <= steps; i++)
        {
            var t = i * stepS;
            points.Add(new TrajectoryPoint { TS = t, TempC = TemperatureAt(scenario, t) });
        }

        if (points.Count == 0 || points[^1].TS + Eps < durationS)
        {
            points.Add(new TrajectoryPoint { TS = durationS, TempC = TemperatureAt(scenario, durationS) });
        }

        return new SimulationOutput
        {
            Scenario = scenario,
            TargetTempC = targetTempC,
            Milestones = milestones,
            Points = points
        };
    }

    public static Milestones ComputeMilestones(ScenarioInput scenario, double targetTempC)
    {
        var reachesZero = ZeroCrossingTime(scenario);
        var freezeComplete = FreezeCompletionTime(scenario, reachesZero);
        var reachesTarget = TargetTime(scenario, targetTempC, freezeComplete);

        string? reason = null;
        if (!reachesTarget.HasValue)
        {
            reason =
                $"target {targetTempC:F2} C is unreachable with ambient {scenario.AmbientTempC:F2} C and current transfer parameters";
        }
        else if (!freezeComplete.HasValue && targetTempC < 0.0)
        {
            reason = "phase change cannot complete under the provided ambient temperature";
        }

        return new Milestones
        {
            ReachesZeroS = reachesZero,
            FreezeCompleteS = freezeComplete,
            ReachesTargetS = reachesTarget,
            ReasonUnreachable = reason
        };
    }

    private static void ValidateScenario(ScenarioInput scenario)
    {
        if (scenario.MassKg <= 0.0) throw new ArgumentException("Mass must be > 0.");
        if (scenario.AreaM2 <= 0.0) throw new ArgumentException("Area must be > 0.");
        if (scenario.HtcWm2K <= 0.0) throw new ArgumentException("HTC must be > 0.");
    }

    private static double TemperatureAt(ScenarioInput scenario, double tS)
    {
        var t = Math.Max(0.0, tS);
        if (scenario.InitialTempC <= 0.0)
        {
            return ExpTemp(scenario.InitialTempC, scenario.AmbientTempC, IceDecay(scenario), t);
        }

        var tZero = ZeroCrossingTime(scenario);
        if (tZero.HasValue)
        {
            if (t <= tZero.Value)
            {
                return ExpTemp(scenario.InitialTempC, scenario.AmbientTempC, LiquidDecay(scenario), t);
            }

            var tFreezeEnd = FreezeCompletionTime(scenario, tZero);
            if (tFreezeEnd.HasValue)
            {
                if (t <= tFreezeEnd.Value)
                {
                    return 0.0;
                }

                return ExpTemp(0.0, scenario.AmbientTempC, IceDecay(scenario), t - tFreezeEnd.Value);
            }
        }

        return ExpTemp(scenario.InitialTempC, scenario.AmbientTempC, LiquidDecay(scenario), t);
    }

    private static double? ZeroCrossingTime(ScenarioInput scenario)
    {
        if (scenario.InitialTempC <= 0.0) return 0.0;
        return CoolingTime(scenario.InitialTempC, scenario.AmbientTempC, 0.0, LiquidDecay(scenario));
    }

    private static double? FreezeCompletionTime(ScenarioInput scenario, double? tZeroS)
    {
        if (scenario.InitialTempC <= 0.0) return 0.0;
        if (!tZeroS.HasValue) return null;

        var latentPower = HeatTransferRate(scenario) * (0.0 - scenario.AmbientTempC);
        if (latentPower <= Eps) return null;
        var latentDuration = scenario.MassKg * scenario.LatentHeatJkg / latentPower;
        return tZeroS.Value + latentDuration;
    }

    private static double? TargetTime(ScenarioInput scenario, double targetTempC, double? freezeCompleteS)
    {
        if (scenario.InitialTempC <= targetTempC) return 0.0;

        if (targetTempC >= 0.0)
        {
            if (scenario.InitialTempC <= 0.0) return 0.0;
            return CoolingTime(scenario.InitialTempC, scenario.AmbientTempC, targetTempC, LiquidDecay(scenario));
        }

        if (scenario.InitialTempC > 0.0)
        {
            if (!freezeCompleteS.HasValue) return null;
            var postFreeze = CoolingTime(0.0, scenario.AmbientTempC, targetTempC, IceDecay(scenario));
            if (!postFreeze.HasValue) return null;
            return freezeCompleteS.Value + postFreeze.Value;
        }

        return CoolingTime(scenario.InitialTempC, scenario.AmbientTempC, targetTempC, IceDecay(scenario));
    }

    private static double HeatTransferRate(ScenarioInput scenario) => scenario.HtcWm2K * scenario.AreaM2;
    private static double LiquidDecay(ScenarioInput scenario) => HeatTransferRate(scenario) / (scenario.MassKg * scenario.CpLiquidJkgK);
    private static double IceDecay(ScenarioInput scenario) => HeatTransferRate(scenario) / (scenario.MassKg * scenario.CpIceJkgK);

    private static double ExpTemp(double startC, double ambientC, double decaySInv, double dtS)
    {
        if (decaySInv <= Eps) return startC;
        return ambientC + (startC - ambientC) * Math.Exp(-decaySInv * Math.Max(0.0, dtS));
    }

    private static double? CoolingTime(double startC, double ambientC, double targetC, double decaySInv)
    {
        if (Math.Abs(targetC - startC) <= Eps) return 0.0;
        if (decaySInv <= Eps) return null;

        var denominator = startC - ambientC;
        if (Math.Abs(denominator) <= Eps) return null;

        var ratio = (targetC - ambientC) / denominator;
        if (ratio <= Eps || ratio > 1.0 + 1e-12) return null;

        var dt = -Math.Log(Math.Min(1.0, ratio)) / decaySInv;
        return double.IsFinite(dt) && dt >= 0.0 ? dt : null;
    }
}
