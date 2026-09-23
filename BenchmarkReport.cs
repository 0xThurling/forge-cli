using System.Text.Json;

namespace forge;

/// <summary>One benchmark from a Google Benchmark report, normalised to nanoseconds.</summary>
internal sealed record BenchmarkResult(string Name, double Nanoseconds, string Unit);

/// <summary>
/// Reads Google Benchmark's JSON output (<c>--benchmark_format=json</c>), which
/// is what <c>forge bench --save</c> writes and <c>--compare</c> reads back.
/// </summary>
/// <remarks>
/// A report contains several entries per benchmark when repetitions are used
/// (aggregates such as the mean and median). One value per name is kept, in
/// order of preference: the mean, then a plain iteration, then whatever is
/// there — which is what a comparison between two runs wants.
/// </remarks>
internal static class BenchmarkReport
{
  public static Dictionary<string, BenchmarkResult> Read(string path)
  {
    var results = new Dictionary<string, BenchmarkResult>(StringComparer.Ordinal);
    var rank = new Dictionary<string, int>(StringComparer.Ordinal);

    try
    {
      if (!File.Exists(path))
        return results;

      using var document = JsonDocument.Parse(File.ReadAllText(path));
      if (!document.RootElement.TryGetProperty("benchmarks", out var benchmarks))
        return results;

      foreach (var entry in benchmarks.EnumerateArray())
      {
        var name = Text(entry, "name");
        if (name.Length == 0)
          continue;

        var aggregate = Text(entry, "aggregate_name");
        var runType = Text(entry, "run_type");

        var preference = aggregate == "mean" ? 0 : runType == "iteration" ? 1 : 2;
        if (rank.TryGetValue(name, out var seen) && seen <= preference)
          continue;

        // Google Benchmark picks a unit per benchmark, so both runs have to be
        // brought to a common one before they can be compared.
        var value = entry.TryGetProperty("real_time", out var realTime) && realTime.ValueKind == JsonValueKind.Number
          ? realTime.GetDouble()
          : entry.TryGetProperty("cpu_time", out var cpuTime) && cpuTime.ValueKind == JsonValueKind.Number
            ? cpuTime.GetDouble()
            : double.NaN;

        if (double.IsNaN(value))
          continue;

        var unit = Text(entry, "time_unit");
        results[name] = new BenchmarkResult(name, ToNanoseconds(value, unit), unit.Length > 0 ? unit : "ns");
        rank[name] = preference;
      }
    }
    catch (Exception)
    {
      return new Dictionary<string, BenchmarkResult>(StringComparer.Ordinal);
    }

    return results;
  }

  private static string Text(JsonElement element, string property) =>
    element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
      ? value.GetString() ?? string.Empty
      : string.Empty;

  private static double ToNanoseconds(double value, string unit) => unit switch
  {
    "s" => value * 1_000_000_000,
    "ms" => value * 1_000_000,
    "us" => value * 1_000,
    _ => value
  };
}
