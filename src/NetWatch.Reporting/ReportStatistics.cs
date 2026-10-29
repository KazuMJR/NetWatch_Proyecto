namespace NetWatch.Reporting;

public sealed record ReportSample(string Category, int DeviceId, string DeviceName, DateTime DateUtc,
    decimal? CpuPercent, decimal? MemoryPercent, decimal? DiskPercent, decimal? ResponseTimeMs);
public sealed record CountStatistic(string Label, int Count, decimal Share);
public sealed record MetricStatistic(string Label, string Unit, int Samples, decimal? Average, decimal? Maximum);
public sealed record PeriodStatistic(DateTime FromUtc, DateTime ToUtc, int Count,
    decimal? CpuAverage = null, decimal? MemoryAverage = null, decimal? DiskAverage = null);

/// <summary>Descriptive statistics of the selected records, never an estimate of uptime.</summary>
public sealed record ReportStatistics(int Records, int Devices, IReadOnlyList<CountStatistic> Categories,
    IReadOnlyList<MetricStatistic> Metrics, IReadOnlyList<CountStatistic> ByDevice, IReadOnlyList<PeriodStatistic> Periods)
{
    public static ReportStatistics Build(IEnumerable<ReportSample> source)
    {
        var rows = source.ToList();
        var metrics = rows.Where(x => x.Category == "Métrica").ToList();
        MetricStatistic Measure(string label, string unit, Func<ReportSample, decimal?> selector)
        {
            var values = metrics.Select(selector).Where(x => x.HasValue).Select(x => x!.Value).ToList();
            return new(label, unit, values.Count, values.Count == 0 ? null : values.Average(), values.Count == 0 ? null : values.Max());
        }
        decimal Share(int count) => rows.Count == 0 ? 0 : count * 100m / rows.Count;
        var categories = new[] { "Métrica", "Evento", "Alerta", "Estado" }
            .Concat(rows.Select(x => x.Category).Where(x => x is not ("Métrica" or "Evento" or "Alerta" or "Estado")).Distinct())
            .Select(label => new CountStatistic(label, rows.Count(x => x.Category == label), Share(rows.Count(x => x.Category == label)))).ToList();
        var devices = rows.GroupBy(x => x.DeviceId)
            .Select(group => new CountStatistic($"{group.First().DeviceName} (#{group.Key})", group.Count(), Share(group.Count())))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label).ToList();
        // Keep the visual compact without dropping records from the calculation.
        if (devices.Count > 6)
        {
            var rest = devices.Skip(6).Sum(x => x.Count);
            devices = devices.Take(6).Append(new CountStatistic("Otros dispositivos", rest, Share(rest))).ToList();
        }
        var periods = new List<PeriodStatistic>();
        if (rows.Count > 0)
        {
            var first = rows.Min(x => x.DateUtc).Date;
            var last = rows.Max(x => x.DateUtc).Date;
            var days = (last - first).Days + 1;
            var bucketDays = Math.Max(1, (int)Math.Ceiling(days / 12d));
            var counts = rows.GroupBy(x => (x.DateUtc.Date - first).Days / bucketDays).ToDictionary(x => x.Key, x => x.Count());
            var groupedMetrics = metrics.GroupBy(x => (x.DateUtc.Date - first).Days / bucketDays).ToDictionary(x => x.Key, x => x.ToList());
            for (var offset = 0; offset < days; offset += bucketDays)
            {
                var bucket = offset / bucketDays;
                var samples = groupedMetrics.GetValueOrDefault(bucket) ?? [];
                periods.Add(new(first.AddDays(offset), first.AddDays(Math.Min(days - 1, offset + bucketDays - 1)), counts.GetValueOrDefault(bucket),
                    samples.Select(x => x.CpuPercent).Average(), samples.Select(x => x.MemoryPercent).Average(), samples.Select(x => x.DiskPercent).Average()));
            }
        }
        return new(rows.Count, rows.Select(x => x.DeviceId).Distinct().Count(), categories,
            [Measure("CPU", "%", x => x.CpuPercent), Measure("RAM", "%", x => x.MemoryPercent),
             Measure("Disco", "%", x => x.DiskPercent), Measure("Respuesta", "ms", x => x.ResponseTimeMs)], devices, periods);
    }
}
