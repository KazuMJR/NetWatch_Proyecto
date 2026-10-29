using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetWatch.API.Contracts;
using NetWatch.API.Services;
using NetWatch.Reporting;

namespace NetWatch.Tests;

public sealed class ReportStatisticsTests
{
    private static readonly DateTime Day = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static ReportSample Sample(int device, int day, decimal? cpu, string category = "Métrica") => new(category, device, "Equipo", Day.AddDays(day), cpu, 50, 30, 10);

    [Fact]
    public void MissingValuesAreNotZeroAndNonMetricsDoNotEnterAverages()
    {
        var stats = ReportStatistics.Build([Sample(1, 0, 0), Sample(1, 1, 100), Sample(2, 1, null), Sample(2, 1, 999, "Alerta")]);
        Assert.Equal(4, stats.Records); Assert.Equal(2, stats.Devices);
        Assert.Equal(50, stats.Metrics[0].Average); Assert.Equal(100, stats.Metrics[0].Maximum); Assert.Equal(2, stats.Metrics[0].Samples);
        Assert.Equal(100, stats.Categories.Sum(x => x.Share));
        Assert.Equal(4, stats.Periods.Sum(x => x.Count));
        Assert.Equal(0, stats.Periods[0].CpuAverage);
        Assert.Equal(100, stats.Periods[1].CpuAverage);
    }

    [Fact]
    public void EmptyAndNonMetricReportsHaveNoFabricatedAverages()
    {
        foreach(var rows in new[] { Array.Empty<ReportSample>(), new[] { Sample(1,0,null,"Evento") } })
        {
            var stats = ReportStatistics.Build(rows);
            Assert.All(stats.Metrics, x => { Assert.Null(x.Average); Assert.Null(x.Maximum); Assert.Equal(0,x.Samples); });
        }
    }

    [Fact]
    public void BucketsIncludeGapsAndOtherDevicesWithoutDroppingRecords()
    {
        var rows = Enumerable.Range(1,10).Select(x => Sample(x, x == 10 ? 364 : 0, 15)).ToList();
        var stats = ReportStatistics.Build(rows);
        Assert.InRange(stats.Periods.Count, 1, 12);
        Assert.Contains(stats.Periods, x => x.Count == 0);
        Assert.All(stats.Periods.Where(x => x.Count == 0), x => Assert.Null(x.CpuAverage));
        Assert.Equal(10, stats.Periods.Sum(x => x.Count));
        Assert.Equal(10, stats.ByDevice.Sum(x => x.Count));
        Assert.Equal(4, stats.ByDevice.Single(x => x.Label == "Otros dispositivos").Count);
    }

    [Fact]
    public void ExportsContainSummaryAndPreserveDetail()
    {
        ReportRowDto[] rows = [new("Métrica", 1, "Equipo", Day, "Prueba", 25,50,30,null,0,10)];
        using var archive = new ZipArchive(new MemoryStream(ExcelReportBuilder.Build(rows,"metrics","Equipo",Day,Day,Day)));
        XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var summary = XDocument.Load(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        Assert.Equal(4,summary.Descendants(ns+"dataBar").Count());
        Assert.Equal("25",summary.Descendants(ns+"c").Single(x=>(string?)x.Attribute("r")=="B16").Element(ns+"v")?.Value);
        var detail=XDocument.Load(archive.GetEntry("xl/worksheets/sheet2.xml")!.Open());
        Assert.Contains(detail.Descendants(ns+"row"),x=>(string?)x.Attribute("r")=="5");
        var pdf=Encoding.Latin1.GetString(PdfReportBuilder.Build(rows,"metrics","Equipo",Day,Day,Day));
        Assert.Contains("Resumen estadístico",pdf); Assert.Contains("/Count 2",pdf);
    }
}
