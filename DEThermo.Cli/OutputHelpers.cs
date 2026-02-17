using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DEThermo.Cli;

internal static class OutputHelpers
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void PrintMilestones(double targetTempC, Milestones milestones)
    {
        Console.WriteLine($"Target temperature: {targetTempC:F2} C");
        Console.WriteLine($"Time to hit 0C: {Physics.FmtSeconds(milestones.ReachesZeroS)}");
        Console.WriteLine($"Time to complete freeze: {Physics.FmtSeconds(milestones.FreezeCompleteS)}");
        Console.WriteLine($"Time to reach target: {Physics.FmtSeconds(milestones.ReachesTargetS)}");
        if (!string.IsNullOrWhiteSpace(milestones.ReasonUnreachable))
        {
            Console.WriteLine($"Note: {milestones.ReasonUnreachable}");
        }
    }

    public static void WriteTrajectoryCsv(string path, List<TrajectoryPoint> points)
    {
        EnsureParent(path);
        var sb = new StringBuilder();
        sb.AppendLine("t_s,temp_c");
        foreach (var p in points)
        {
            sb.AppendLine($"{p.TS.ToString(Invariant)},{p.TempC.ToString(Invariant)}");
        }

        File.WriteAllText(path, sb.ToString());
    }

    public static void WriteBatchCsv(string path, List<BatchRecord> records)
    {
        EnsureParent(path);
        var sb = new StringBuilder();
        sb.AppendLine("name,reaches_zero_s,freeze_complete_s,reaches_target_s,reason_unreachable");
        foreach (var r in records.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(
                $"{CsvEscape(r.Name)},{FmtCsv(r.ReachesZeroS)},{FmtCsv(r.FreezeCompleteS)},{FmtCsv(r.ReachesTargetS)},{CsvEscape(r.ReasonUnreachable ?? string.Empty)}");
        }

        File.WriteAllText(path, sb.ToString());
    }

    public static void WriteJson(string path, object payload)
    {
        EnsureParent(path);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions()));
    }

    public static void WriteText(string path, string content)
    {
        EnsureParent(path);
        File.WriteAllText(path, content);
    }

    public static T ReadJson<T>(string path)
    {
        var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions());
        if (value is null)
        {
            throw new InvalidOperationException($"Failed to parse JSON at {path}");
        }

        return value;
    }

    public static string BuildBatchReport(List<BatchRecord> records, double targetTempC)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# DE-Thermo Batch Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- scenarios: {records.Count}");
        sb.AppendLine($"- freeze complete (valid): {records.Count(r => r.FreezeCompleteS.HasValue)}");
        sb.AppendLine($"- reaches target {targetTempC:F1} C (valid): {records.Count(r => r.ReachesTargetS.HasValue)}");
        sb.AppendLine();

        var best = records.Where(r => r.FreezeCompleteS.HasValue).OrderBy(r => r.FreezeCompleteS!.Value).FirstOrDefault();
        if (best is not null)
        {
            sb.AppendLine("## Best Performer");
            sb.AppendLine(
                $"- {best.Name} reaches full freeze in {best.FreezeCompleteS!.Value:F1} s ({best.FreezeCompleteS!.Value / 60.0:F1} min)");
            sb.AppendLine();
        }

        sb.AppendLine("## Detailed Results");
        sb.AppendLine("| Scenario | t(0C) s | t(freeze complete) s | t(target) s | Notes |");
        sb.AppendLine("|---|---:|---:|---:|---|");
        foreach (var row in records.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(
                $"| {EscapePipe(row.Name)} | {FmtFlat(row.ReachesZeroS)} | {FmtFlat(row.FreezeCompleteS)} | {FmtFlat(row.ReachesTargetS)} | {EscapePipe(row.ReasonUnreachable ?? "-")} |");
        }

        return sb.ToString();
    }

    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };

    private static void EnsureParent(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    private static string FmtFlat(double? value) => value.HasValue ? value.Value.ToString("F1", Invariant) : "N/A";

    private static string FmtCsv(double? value) => value.HasValue ? value.Value.ToString(Invariant) : string.Empty;

    private static string CsvEscape(string text)
    {
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
    }

    private static string EscapePipe(string text) => text.Replace("|", "\\|");
}
