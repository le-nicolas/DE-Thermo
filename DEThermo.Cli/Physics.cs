using System.Collections.Concurrent;
using System.Globalization;

namespace DEThermo.Cli;

internal static class Physics
{
    private const double Eps = 1e-9;

    public static void ValidateScenario(Scenario scenario)
    {
        if (scenario.MassKg <= 0.0) throw new ArgumentException("mass-kg must be > 0");
        if (scenario.AreaM2 <= 0.0) throw new ArgumentException("area-m2 must be > 0");
        if (scenario.HtcWm2K <= 0.0) throw new ArgumentException("htc-w-m2k must be > 0");
        if (scenario.CpLiquidJkgK <= 0.0 || scenario.CpIceJkgK <= 0.0)
            throw new ArgumentException("heat capacities must be > 0");
        if (scenario.LatentHeatJkg < 0.0) throw new ArgumentException("latent heat must be >= 0");
    }

    public static SimulationOutput SimulateTrajectory(Scenario scenario, double targetTempC, double durationS, double stepS)
    {
        var milestones = ComputeMilestones(scenario, targetTempC);
        var points = new List<TrajectoryPoint>();
        var stepCount = (int)Math.Ceiling(durationS / stepS);
        for (var i = 0; i <= stepCount; i++)
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

    public static Milestones ComputeMilestones(Scenario scenario, double targetTempC)
    {
        var reachesZeroS = ZeroCrossingTime(scenario);
        var freezeCompleteS = FreezeCompletionTime(scenario, reachesZeroS);
        var reachesTargetS = TargetTimeSeconds(scenario, targetTempC, freezeCompleteS);

        string? reason = null;
        if (!reachesTargetS.HasValue)
        {
            reason =
                $"target {targetTempC:F2} C is unreachable with ambient {scenario.AmbientTempC:F2} C and current transfer parameters";
        }
        else if (!freezeCompleteS.HasValue && targetTempC < 0.0)
        {
            reason = "phase change cannot complete under the provided ambient temperature";
        }

        return new Milestones
        {
            ReachesZeroS = reachesZeroS,
            FreezeCompleteS = freezeCompleteS,
            ReachesTargetS = reachesTargetS,
            ReasonUnreachable = reason
        };
    }

    public static double? FindMinHtcForDeadline(Scenario baseScenario, double deadlineS, double htcMin, double htcMax,
        int iterations)
    {
        if (baseScenario.InitialTempC <= 0.0)
        {
            return Math.Max(1e-6, htcMin);
        }

        var low = Math.Max(1e-6, htcMin);
        var high = Math.Max(low, htcMax);
        var upper = CloneScenario(baseScenario);
        upper.HtcWm2K = high;

        var upperFreeze = ComputeMilestones(upper, -0.1).FreezeCompleteS;
        if (!upperFreeze.HasValue || upperFreeze.Value > deadlineS)
        {
            return null;
        }

        var loops = Math.Max(20, iterations);
        for (var i = 0; i < loops; i++)
        {
            var mid = 0.5 * (low + high);
            var probe = CloneScenario(baseScenario);
            probe.HtcWm2K = mid;
            var freeze = ComputeMilestones(probe, -0.1).FreezeCompleteS;
            if (freeze.HasValue && freeze.Value <= deadlineS)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }

        return high;
    }

    public static MonteCarloSummary RunMonteCarloSimulation(
        Scenario baseScenario,
        double targetTempC,
        double deadlineS,
        MonteCarloConfig config)
    {
        var freezeTimes = new ConcurrentBag<double>();
        var targetTimes = new ConcurrentBag<double>();
        var freezeHits = 0;
        var targetHits = 0;

        Parallel.For(0, config.Samples, index =>
        {
            var rng = new Random(unchecked(config.Seed + index * 7919));
            var sample = CloneScenario(baseScenario);
            sample.MassKg = Math.Max(1e-6, baseScenario.MassKg * (1.0 + Math.Max(0.0, config.MassCv) * NextGaussian(rng)));
            sample.HtcWm2K =
                Math.Max(1e-6, baseScenario.HtcWm2K * (1.0 + Math.Max(0.0, config.HtcCv) * NextGaussian(rng)));
            sample.AreaM2 = Math.Max(1e-6, baseScenario.AreaM2 * (1.0 + Math.Max(0.0, config.AreaCv) * NextGaussian(rng)));
            sample.InitialTempC = baseScenario.InitialTempC + Math.Max(0.0, config.InitialTempStdC) * NextGaussian(rng);
            sample.AmbientTempC = baseScenario.AmbientTempC + Math.Max(0.0, config.AmbientTempStdC) * NextGaussian(rng);

            var milestones = ComputeMilestones(sample, targetTempC);
            if (milestones.FreezeCompleteS.HasValue)
            {
                freezeTimes.Add(milestones.FreezeCompleteS.Value);
                if (milestones.FreezeCompleteS.Value <= deadlineS)
                {
                    Interlocked.Increment(ref freezeHits);
                }
            }

            if (milestones.ReachesTargetS.HasValue)
            {
                targetTimes.Add(milestones.ReachesTargetS.Value);
                if (milestones.ReachesTargetS.Value <= deadlineS)
                {
                    Interlocked.Increment(ref targetHits);
                }
            }
        });

        var freezeStats = MeanStd(freezeTimes.ToList());
        var targetStats = MeanStd(targetTimes.ToList());
        return new MonteCarloSummary
        {
            Samples = config.Samples,
            DeadlineS = deadlineS,
            FreezeByDeadlineProbability = freezeHits / (double)config.Samples,
            TargetByDeadlineProbability = targetHits / (double)config.Samples,
            FreezeTimeMeanS = freezeStats.mean,
            FreezeTimeStdS = freezeStats.std,
            TargetTimeMeanS = targetStats.mean,
            TargetTimeStdS = targetStats.std
        };
    }

    public static string FmtSeconds(double? seconds)
    {
        return seconds.HasValue ? $"{seconds.Value:F1} s ({seconds.Value / 60.0:F1} min)" : "N/A";
    }

    public static string Fmt(double? value)
    {
        return value.HasValue ? value.Value.ToString("F1", CultureInfo.InvariantCulture) : "N/A";
    }

    private static double? ZeroCrossingTime(Scenario scenario)
    {
        if (scenario.InitialTempC <= 0.0)
        {
            return 0.0;
        }

        return CoolingTimeSeconds(
            scenario.InitialTempC,
            scenario.AmbientTempC,
            0.0,
            LiquidDecay(scenario));
    }

    private static double? FreezeCompletionTime(Scenario scenario, double? tZeroS)
    {
        if (scenario.InitialTempC <= 0.0)
        {
            return 0.0;
        }

        if (!tZeroS.HasValue)
        {
            return null;
        }

        var latentPowerW = HeatTransferRate(scenario) * (0.0 - scenario.AmbientTempC);
        if (latentPowerW <= Eps)
        {
            return null;
        }

        var latentDurationS = scenario.MassKg * scenario.LatentHeatJkg / latentPowerW;
        return tZeroS.Value + latentDurationS;
    }

    private static double? TargetTimeSeconds(Scenario scenario, double targetTempC, double? freezeCompleteS)
    {
        if (scenario.InitialTempC <= targetTempC)
        {
            return 0.0;
        }

        if (targetTempC >= 0.0)
        {
            if (scenario.InitialTempC <= 0.0)
            {
                return 0.0;
            }

            return CoolingTimeSeconds(
                scenario.InitialTempC,
                scenario.AmbientTempC,
                targetTempC,
                LiquidDecay(scenario));
        }

        if (scenario.InitialTempC > 0.0)
        {
            if (!freezeCompleteS.HasValue)
            {
                return null;
            }

            var postFreeze = CoolingTimeSeconds(
                0.0,
                scenario.AmbientTempC,
                targetTempC,
                IceDecay(scenario));
            if (!postFreeze.HasValue)
            {
                return null;
            }

            return freezeCompleteS.Value + postFreeze.Value;
        }

        return CoolingTimeSeconds(
            scenario.InitialTempC,
            scenario.AmbientTempC,
            targetTempC,
            IceDecay(scenario));
    }

    private static double TemperatureAt(Scenario scenario, double tS)
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

    private static double HeatTransferRate(Scenario scenario) => scenario.HtcWm2K * scenario.AreaM2;

    private static double LiquidDecay(Scenario scenario) => HeatTransferRate(scenario) / (scenario.MassKg * scenario.CpLiquidJkgK);

    private static double IceDecay(Scenario scenario) => HeatTransferRate(scenario) / (scenario.MassKg * scenario.CpIceJkgK);

    private static double ExpTemp(double startC, double ambientC, double decaySInv, double dtS)
    {
        if (decaySInv <= Eps)
        {
            return startC;
        }

        return ambientC + (startC - ambientC) * Math.Exp(-decaySInv * Math.Max(0.0, dtS));
    }

    private static double? CoolingTimeSeconds(double startC, double ambientC, double targetC, double decaySInv)
    {
        if (Math.Abs(targetC - startC) <= Eps)
        {
            return 0.0;
        }
        if (decaySInv <= Eps)
        {
            return null;
        }

        var denominator = startC - ambientC;
        if (Math.Abs(denominator) <= Eps)
        {
            return null;
        }

        var ratio = (targetC - ambientC) / denominator;
        if (ratio <= Eps || ratio > 1.0 + 1e-12)
        {
            return null;
        }

        var dt = -Math.Log(Math.Min(1.0, ratio)) / decaySInv;
        if (double.IsFinite(dt) && dt >= 0.0)
        {
            return dt;
        }

        return null;
    }

    private static (double? mean, double? std) MeanStd(List<double> values)
    {
        if (values.Count == 0)
        {
            return (null, null);
        }

        var mean = values.Average();
        var variance = values.Select(v => (v - mean) * (v - mean)).Average();
        return (mean, Math.Sqrt(variance));
    }

    private static double NextGaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static Scenario CloneScenario(Scenario scenario)
    {
        return new Scenario
        {
            Name = scenario.Name,
            MassKg = scenario.MassKg,
            InitialTempC = scenario.InitialTempC,
            AmbientTempC = scenario.AmbientTempC,
            AreaM2 = scenario.AreaM2,
            HtcWm2K = scenario.HtcWm2K,
            CpLiquidJkgK = scenario.CpLiquidJkgK,
            CpIceJkgK = scenario.CpIceJkgK,
            LatentHeatJkg = scenario.LatentHeatJkg
        };
    }
}
