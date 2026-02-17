using System.Collections.Concurrent;

namespace DEThermo.Cli;

internal static class CliCommands
{
    public static void RunSimulate(OptionMap options)
    {
        var scenario = BuildScenario(options);
        Physics.ValidateScenario(scenario);

        var targetTempC = options.GetDouble("target-temp-c", -18.0);
        var durationS = options.GetDouble("duration-s", 21_600.0);
        var stepS = options.GetDouble("step-s", 30.0);
        if (durationS <= 0.0) throw new ArgumentException("--duration-s must be > 0");
        if (stepS <= 0.0) throw new ArgumentException("--step-s must be > 0");

        var output = Physics.SimulateTrajectory(scenario, targetTempC, durationS, stepS);
        Console.WriteLine($"Scenario: {scenario.Name ?? "unnamed scenario"}");
        OutputHelpers.PrintMilestones(targetTempC, output.Milestones);
        Console.WriteLine($"Generated {output.Points.Count} trajectory points");

        var csvOutput = options.GetStringOrNull("csv-output");
        if (!string.IsNullOrWhiteSpace(csvOutput))
        {
            OutputHelpers.WriteTrajectoryCsv(csvOutput!, output.Points);
            Console.WriteLine($"Wrote trajectory CSV to {csvOutput}");
        }

        var jsonOutput = options.GetStringOrNull("json-output");
        if (!string.IsNullOrWhiteSpace(jsonOutput))
        {
            OutputHelpers.WriteJson(jsonOutput!, output);
            Console.WriteLine($"Wrote simulation JSON to {jsonOutput}");
        }
    }

    public static void RunBatch(OptionMap options)
    {
        var inputPath = options.GetRequiredString("input");
        var batch = OutputHelpers.ReadJson<BatchInput>(inputPath);
        if (batch.Scenarios is null || batch.Scenarios.Count == 0)
        {
            throw new ArgumentException("Batch input has no scenarios.");
        }

        var targetTempC = batch.TargetTempC ?? -18.0;
        var records = new ConcurrentBag<BatchRecord>();
        Parallel.ForEach(batch.Scenarios.Select((scenario, index) => (scenario, index)), entry =>
        {
            var scenario = entry.scenario;
            scenario.Name ??= $"scenario_{entry.index + 1}";
            Physics.ValidateScenario(scenario);
            var milestones = Physics.ComputeMilestones(scenario, targetTempC);
            records.Add(new BatchRecord
            {
                Name = scenario.Name!,
                ReachesZeroS = milestones.ReachesZeroS,
                FreezeCompleteS = milestones.FreezeCompleteS,
                ReachesTargetS = milestones.ReachesTargetS,
                ReasonUnreachable = milestones.ReasonUnreachable
            });
        });

        var ordered = records.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Console.WriteLine(
            $"Batch processed: {ordered.Count} scenarios | freeze-valid: {ordered.Count(r => r.FreezeCompleteS.HasValue)} | target-valid: {ordered.Count(r => r.ReachesTargetS.HasValue)}");

        var csvOutput = options.GetStringOrNull("csv-output");
        if (!string.IsNullOrWhiteSpace(csvOutput))
        {
            OutputHelpers.WriteBatchCsv(csvOutput!, ordered);
            Console.WriteLine($"Wrote batch CSV to {csvOutput}");
        }

        var jsonOutput = options.GetStringOrNull("json-output");
        if (!string.IsNullOrWhiteSpace(jsonOutput))
        {
            var payload = new BatchOutput
            {
                Input = inputPath,
                TargetTempC = targetTempC,
                DurationS = batch.DurationS ?? 21_600.0,
                StepS = batch.StepS ?? 60.0,
                Records = ordered
            };
            OutputHelpers.WriteJson(jsonOutput!, payload);
            Console.WriteLine($"Wrote batch JSON to {jsonOutput}");
        }

        var reportOutput = options.GetStringOrNull("report-output");
        if (!string.IsNullOrWhiteSpace(reportOutput))
        {
            var report = OutputHelpers.BuildBatchReport(ordered, targetTempC);
            OutputHelpers.WriteText(reportOutput!, report);
            Console.WriteLine($"Wrote batch report to {reportOutput}");
        }
    }

    public static void RunOptimize(OptionMap options)
    {
        var freezeDeadlineS = options.GetRequiredDouble("freeze-deadline-s");
        var htcMin = options.GetDouble("htc-min", 1.0);
        var htcMax = options.GetDouble("htc-max", 80.0);
        var iterations = options.GetInt("iterations", 64);
        if (freezeDeadlineS <= 0.0) throw new ArgumentException("--freeze-deadline-s must be > 0");
        if (htcMin <= 0.0 || htcMax <= 0.0 || htcMax < htcMin)
            throw new ArgumentException("Require 0 < htc-min <= htc-max");
        if (iterations < 1) throw new ArgumentException("--iterations must be >= 1");

        var baseScenario = new Scenario
        {
            Name = "optimization_base",
            MassKg = options.GetRequiredDouble("mass-kg"),
            InitialTempC = options.GetRequiredDouble("initial-temp-c"),
            AmbientTempC = options.GetRequiredDouble("ambient-temp-c"),
            AreaM2 = options.GetRequiredDouble("area-m2"),
            HtcWm2K = htcMin,
            CpLiquidJkgK = 4186.0,
            CpIceJkgK = 2100.0,
            LatentHeatJkg = 333_700.0
        };
        Physics.ValidateScenario(baseScenario);

        var minHtc = Physics.FindMinHtcForDeadline(baseScenario, freezeDeadlineS, htcMin, htcMax, iterations);
        OptimizeResult result;
        if (minHtc.HasValue)
        {
            var scenario = CloneScenario(baseScenario);
            scenario.HtcWm2K = minHtc.Value;
            var achieved = Physics.ComputeMilestones(scenario, -0.1).FreezeCompleteS;
            result = new OptimizeResult
            {
                FreezeDeadlineS = freezeDeadlineS,
                MinHtcWm2K = minHtc,
                AchievedFreezeS = achieved,
                Feasible = achieved.HasValue && achieved.Value <= freezeDeadlineS
            };

            Console.WriteLine(
                $"Minimum HTC for freeze deadline {freezeDeadlineS:F1} s: {minHtc.Value:F4} W/(m^2*K)");
            Console.WriteLine($"Predicted freeze completion at {achieved:F1} s");
        }
        else
        {
            result = new OptimizeResult
            {
                FreezeDeadlineS = freezeDeadlineS,
                MinHtcWm2K = null,
                AchievedFreezeS = null,
                Feasible = false
            };
            Console.WriteLine(
                $"No feasible HTC in [{htcMin:F3}, {htcMax:F3}] W/(m^2*K) for deadline {freezeDeadlineS:F1} s");
        }

        var jsonOutput = options.GetStringOrNull("json-output");
        if (!string.IsNullOrWhiteSpace(jsonOutput))
        {
            OutputHelpers.WriteJson(jsonOutput!, result);
            Console.WriteLine($"Wrote optimization JSON to {jsonOutput}");
        }
    }

    public static void RunMonteCarlo(OptionMap options)
    {
        var scenario = BuildScenario(options);
        Physics.ValidateScenario(scenario);

        var targetTempC = options.GetDouble("target-temp-c", -18.0);
        var deadlineS = options.GetRequiredDouble("deadline-s");
        var config = new MonteCarloConfig
        {
            Samples = options.GetInt("samples", 2000),
            Seed = options.GetInt("seed", 42),
            MassCv = options.GetDouble("mass-cv", 0.05),
            HtcCv = options.GetDouble("htc-cv", 0.10),
            AreaCv = options.GetDouble("area-cv", 0.05),
            InitialTempStdC = options.GetDouble("initial-temp-std-c", 1.0),
            AmbientTempStdC = options.GetDouble("ambient-temp-std-c", 1.0)
        };
        if (config.Samples < 1) throw new ArgumentException("--samples must be >= 1");

        var summary = Physics.RunMonteCarloSimulation(scenario, targetTempC, deadlineS, config);
        Console.WriteLine($"P(freeze by {deadlineS:F1}s) = {summary.FreezeByDeadlineProbability:F3}");
        Console.WriteLine($"P(target by {deadlineS:F1}s) = {summary.TargetByDeadlineProbability:F3}");
        Console.WriteLine($"Freeze time mean/std: {Physics.Fmt(summary.FreezeTimeMeanS)} / {Physics.Fmt(summary.FreezeTimeStdS)} s");
        Console.WriteLine($"Target time mean/std: {Physics.Fmt(summary.TargetTimeMeanS)} / {Physics.Fmt(summary.TargetTimeStdS)} s");

        var jsonOutput = options.GetStringOrNull("json-output");
        if (!string.IsNullOrWhiteSpace(jsonOutput))
        {
            OutputHelpers.WriteJson(jsonOutput!, summary);
            Console.WriteLine($"Wrote Monte Carlo JSON to {jsonOutput}");
        }
    }

    private static Scenario BuildScenario(OptionMap options)
    {
        return new Scenario
        {
            Name = options.GetStringOrNull("name"),
            MassKg = options.GetRequiredDouble("mass-kg"),
            InitialTempC = options.GetRequiredDouble("initial-temp-c"),
            AmbientTempC = options.GetRequiredDouble("ambient-temp-c"),
            AreaM2 = options.GetRequiredDouble("area-m2"),
            HtcWm2K = options.GetRequiredDouble("htc-w-m2k"),
            CpLiquidJkgK = 4186.0,
            CpIceJkgK = 2100.0,
            LatentHeatJkg = 333_700.0
        };
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
