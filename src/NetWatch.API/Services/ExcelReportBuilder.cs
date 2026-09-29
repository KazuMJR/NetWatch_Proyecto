using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using NetWatch.API.Contracts;

namespace NetWatch.API.Services;

public static class ExcelReportBuilder
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private enum SheetKind { Metrics, Events, Alerts, States }
    private enum CellKind { Text, Number, Integer, Percentage, Date, WrappedText }
    private sealed record ExcelCell(object? Value, CellKind Kind = CellKind.Text, int? Style = null);
    private sealed record DataSheet(string Name, string Color, SheetKind Kind, IReadOnlyList<ReportRowDto> Rows);

    public static byte[] Build(IReadOnlyList<ReportRowDto> rows, string reportType, string deviceLabel, DateTime fromUtc, DateTime toUtc, DateTime generatedAtUtc)
    {
        var normalizedType = reportType.ToLowerInvariant();
        var dataSheets = new List<DataSheet>();

        if (normalizedType is "all" or "metrics")
            dataSheets.Add(new("Métricas", "FF0F9F8F", SheetKind.Metrics, rows.Where(x => x.Category == "Métrica").ToList()));
        if (normalizedType is "all" or "events")
            dataSheets.Add(new("Eventos", "FF2563EB", SheetKind.Events, rows.Where(x => x.Category == "Evento").ToList()));
        if (normalizedType is "all" or "alerts")
            dataSheets.Add(new("Alertas", "FFC33D4A", SheetKind.Alerts, rows.Where(x => x.Category == "Alerta").ToList()));
        if (normalizedType is "all" or "states")
            dataSheets.Add(new("Estados", "FF7C3AED", SheetKind.States, rows.Where(x => x.Category == "Estado").ToList()));

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            WriteTextEntry(archive, "[Content_Types].xml", ContentTypes(dataSheets.Count + 1));
            WriteTextEntry(archive, "_rels/.rels", RootRelationships());
            WriteTextEntry(archive, "docProps/core.xml", CoreProperties(generatedAtUtc));
            WriteTextEntry(archive, "docProps/app.xml", AppProperties());
            WriteTextEntry(archive, "xl/workbook.xml", Workbook(dataSheets));
            WriteTextEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(dataSheets.Count + 1));
            WriteTextEntry(archive, "xl/styles.xml", Styles());
            WriteXmlEntry(archive, "xl/worksheets/sheet1.xml", writer => WriteSummarySheet(writer, rows, dataSheets, deviceLabel, fromUtc, toUtc, generatedAtUtc));

            for (var index = 0; index < dataSheets.Count; index++)
            {
                var sheet = dataSheets[index];
                WriteXmlEntry(archive, $"xl/worksheets/sheet{index + 2}.xml", writer => WriteDataSheet(writer, sheet, deviceLabel, fromUtc, toUtc));
            }
        }
        return output.ToArray();
    }

    private static void WriteSummarySheet(XmlWriter writer, IReadOnlyList<ReportRowDto> rows, IReadOnlyList<DataSheet> sheets, string deviceLabel, DateTime fromUtc, DateTime toUtc, DateTime generatedAtUtc)
    {
        StartWorksheet(writer, "FF0B1F33", "A1:H13", false);
        WriteColumns(writer, 18, 18, 18, 18, 18, 18, 20, 20);
        writer.WriteStartElement("sheetData", SpreadsheetNs);

        WriteRow(writer, 1, [new("Reporte de monitoreo NetWatch", CellKind.Text, 1)], 30);
        WriteRow(writer, 2, [new($"Información consolidada del {fromUtc:dd/MM/yyyy} al {toUtc:dd/MM/yyyy}", CellKind.Text, 2)], 22);
        WriteRow(writer, 4,
        [
            new("DISPOSITIVO", CellKind.Text, 16), new(""),
            new("PERIODO", CellKind.Text, 16), new(""),
            new("TOTAL DE REGISTROS", CellKind.Text, 16), new(""),
            new("GENERADO UTC", CellKind.Text, 16), new("")
        ], 21);
        WriteRow(writer, 5,
        [
            new(deviceLabel, CellKind.Text, 17), new(""),
            new($"{fromUtc:dd/MM/yyyy} - {toUtc:dd/MM/yyyy}", CellKind.Text, 17), new(""),
            new(rows.Count, CellKind.Integer, 17), new(""),
            new(generatedAtUtc.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture), CellKind.Text, 17), new("")
        ], 26);
        WriteRow(writer, 7, [new("Distribución de la información", CellKind.Text, 18)], 24);
        WriteRow(writer, 8,
        [
            new("Tipo de información", CellKind.Text, 3),
            new("Registros", CellKind.Text, 3),
            new("Participación", CellKind.Text, 3),
            new("Hoja de detalle", CellKind.Text, 3)
        ], 24);

        var total = Math.Max(rows.Count, 1);
        var summaryRows = new[]
        {
            ("Métricas de rendimiento", rows.Count(x => x.Category == "Métrica"), "Métricas"),
            ("Eventos del sistema", rows.Count(x => x.Category == "Evento"), "Eventos"),
            ("Alertas generadas", rows.Count(x => x.Category == "Alerta"), "Alertas"),
            ("Cambios de estado", rows.Count(x => x.Category == "Estado"), "Estados")
        };

        for (var index = 0; index < summaryRows.Length; index++)
        {
            var item = summaryRows[index];
            var alt = index % 2 == 1;
            var sheetExists = sheets.Any(x => x.Name == item.Item3);
            WriteRow(writer, 9 + index,
            [
                new(item.Item1, CellKind.Text, alt ? 5 : 4),
                new(item.Item2, CellKind.Integer, alt ? 22 : 21),
                new(rows.Count == 0 ? 0m : (decimal)item.Item2 / total, CellKind.Percentage, alt ? 11 : 10),
                new(sheetExists ? item.Item3 : "No incluida en este reporte", CellKind.Text, alt ? 5 : 4)
            ], 21);
        }

        writer.WriteEndElement();
        WriteMergeCells(writer, "A1:H1", "A2:H2", "A4:B4", "C4:D4", "E4:F4", "G4:H4", "A5:B5", "C5:D5", "E5:F5", "G5:H5", "A7:H7");
        WritePageSettings(writer);
        writer.WriteEndElement();
    }

    private static void WriteDataSheet(XmlWriter writer, DataSheet sheet, string deviceLabel, DateTime fromUtc, DateTime toUtc)
    {
        var headers = Headers(sheet.Kind);
        var widths = Widths(sheet.Kind);
        var lastColumn = ColumnName(headers.Length);
        var lastRow = Math.Max(4, sheet.Rows.Count + 4);

        StartWorksheet(writer, sheet.Color, $"A1:{lastColumn}{lastRow}", true);
        WriteColumns(writer, widths);
        writer.WriteStartElement("sheetData", SpreadsheetNs);
        WriteRow(writer, 1, [new(sheet.Name + " de NetWatch", CellKind.Text, 1)], 30);
        WriteRow(writer, 2, [new($"{deviceLabel} | {fromUtc:dd/MM/yyyy} - {toUtc:dd/MM/yyyy} | {sheet.Rows.Count:N0} registros", CellKind.Text, 2)], 22);
        WriteRow(writer, 4, headers.Select(x => new ExcelCell(x, CellKind.Text, 3)).ToArray(), 26);

        for (var index = 0; index < sheet.Rows.Count; index++)
        {
            var row = sheet.Rows[index];
            var alt = index % 2 == 1;
            WriteRow(writer, index + 5, DataCells(sheet.Kind, row, alt), sheet.Kind == SheetKind.Metrics ? 21 : 28);
        }

        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", SpreadsheetNs);
        writer.WriteAttributeString("ref", $"A4:{lastColumn}{lastRow}");
        writer.WriteEndElement();
        WriteMergeCells(writer, $"A1:{lastColumn}1", $"A2:{lastColumn}2");
        WritePageSettings(writer);
        writer.WriteEndElement();
    }

    private static ExcelCell[] DataCells(SheetKind kind, ReportRowDto row, bool alt)
    {
        var textStyle = alt ? 5 : 4;
        var dateStyle = alt ? 7 : 6;
        var intStyle = alt ? 22 : 21;
        var numberStyle = alt ? 9 : 8;
        var percentStyle = alt ? 11 : 10;
        var wrapStyle = alt ? 20 : 19;
        var common = new List<ExcelCell>
        {
            new(row.DateUtc, CellKind.Date, dateStyle),
            new(row.DeviceId, CellKind.Integer, intStyle),
            new(row.DeviceName, CellKind.Text, textStyle)
        };

        switch (kind)
        {
            case SheetKind.Metrics:
                common.Add(new(row.CpuPercent.HasValue ? row.CpuPercent.Value / 100m : null, CellKind.Percentage, MetricPercentStyle(row.CpuPercent, percentStyle)));
                common.Add(new(row.MemoryPercent.HasValue ? row.MemoryPercent.Value / 100m : null, CellKind.Percentage, MetricPercentStyle(row.MemoryPercent, percentStyle)));
                common.Add(new(row.DiskPercent.HasValue ? row.DiskPercent.Value / 100m : null, CellKind.Percentage, MetricPercentStyle(row.DiskPercent, percentStyle)));
                common.Add(new(row.TemperatureCelsius, CellKind.Number, MetricNumberStyle(row.TemperatureCelsius, numberStyle, 70, 80)));
                common.Add(new(row.NetworkTrafficMbps, CellKind.Number, numberStyle));
                common.Add(new(row.ResponseTimeMs, CellKind.Number, MetricNumberStyle(row.ResponseTimeMs, numberStyle, 100, 200)));
                break;
            case SheetKind.Events:
            {
                var parts = Split(row.Detail, 3);
                common.Add(new(parts[0], CellKind.Text, textStyle));
                common.Add(new(parts[1], CellKind.Text, SeverityStyle(parts[1], alt)));
                common.Add(new(parts[2], CellKind.WrappedText, wrapStyle));
                break;
            }
            case SheetKind.Alerts:
            {
                var parts = Split(row.Detail, 4);
                common.Add(new(parts[0], CellKind.Text, textStyle));
                common.Add(new(parts[1], CellKind.Text, SeverityStyle(parts[1], alt)));
                common.Add(new(parts[2], CellKind.Text, textStyle));
                common.Add(new(parts[3], CellKind.WrappedText, wrapStyle));
                break;
            }
            case SheetKind.States:
            {
                var parts = Split(row.Detail, 2);
                common.Add(new(parts[0], CellKind.Text, textStyle));
                common.Add(new(parts[1], CellKind.WrappedText, wrapStyle));
                break;
            }
        }
        return common.ToArray();
    }

    private static string[] Split(string value, int count)
    {
        var parts = value.Split(" | ", count, StringSplitOptions.None);
        if (parts.Length == count) return parts;
        return parts.Concat(Enumerable.Repeat(string.Empty, count - parts.Length)).ToArray();
    }

    private static int MetricPercentStyle(decimal? value, int normalStyle) => value switch
    {
        >= 90 => 13,
        >= 75 => 12,
        _ => normalStyle
    };

    private static int MetricNumberStyle(decimal? value, int normalStyle, decimal warning, decimal critical) => value switch
    {
        _ when value >= critical => 15,
        _ when value >= warning => 14,
        _ => normalStyle
    };

    private static int SeverityStyle(string value, bool alt) => value switch
    {
        "Crítica" or "Alta" => 23,
        "Advertencia" or "Media" => 24,
        _ => alt ? 5 : 4
    };

    private static string[] Headers(SheetKind kind) => kind switch
    {
        SheetKind.Metrics => ["Fecha UTC", "ID", "Dispositivo", "CPU", "RAM", "Disco", "Temperatura (°C)", "Tráfico (Mbps)", "Respuesta (ms)"],
        SheetKind.Events => ["Fecha UTC", "ID", "Dispositivo", "Tipo", "Severidad", "Descripción"],
        SheetKind.Alerts => ["Fecha UTC", "ID", "Dispositivo", "Alerta", "Nivel", "Estado", "Descripción"],
        _ => ["Fecha UTC", "ID", "Dispositivo", "Cambio de estado", "Motivo"]
    };

    private static double[] Widths(SheetKind kind) => kind switch
    {
        SheetKind.Metrics => [22, 9, 25, 13, 13, 13, 18, 18, 18],
        SheetKind.Events => [22, 9, 25, 20, 16, 58],
        SheetKind.Alerts => [22, 9, 25, 30, 16, 16, 58],
        _ => [22, 9, 25, 28, 58]
    };

    private static void StartWorksheet(XmlWriter writer, string tabColor, string dimension, bool freezeHeaders)
    {
        writer.WriteStartDocument(true);
        writer.WriteStartElement("worksheet", SpreadsheetNs);
        writer.WriteStartElement("sheetPr", SpreadsheetNs);
        writer.WriteStartElement("tabColor", SpreadsheetNs);
        writer.WriteAttributeString("rgb", tabColor);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("dimension", SpreadsheetNs);
        writer.WriteAttributeString("ref", dimension);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews", SpreadsheetNs);
        writer.WriteStartElement("sheetView", SpreadsheetNs);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteAttributeString("showGridLines", "0");
        if (freezeHeaders)
        {
            writer.WriteStartElement("pane", SpreadsheetNs);
            writer.WriteAttributeString("ySplit", "4");
            writer.WriteAttributeString("topLeftCell", "A5");
            writer.WriteAttributeString("activePane", "bottomLeft");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr", SpreadsheetNs);
        writer.WriteAttributeString("defaultRowHeight", "18");
        writer.WriteEndElement();
    }

    private static void WriteColumns(XmlWriter writer, params double[] widths)
    {
        writer.WriteStartElement("cols", SpreadsheetNs);
        for (var index = 0; index < widths.Length; index++)
        {
            writer.WriteStartElement("col", SpreadsheetNs);
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", widths[index].ToString("0.##", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, int rowNumber, IReadOnlyList<ExcelCell> cells, double height)
    {
        writer.WriteStartElement("row", SpreadsheetNs);
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("ht", height.ToString("0.##", CultureInfo.InvariantCulture));
        writer.WriteAttributeString("customHeight", "1");
        for (var index = 0; index < cells.Count; index++) WriteCell(writer, rowNumber, index + 1, cells[index]);
        writer.WriteEndElement();
    }

    private static void WriteCell(XmlWriter writer, int row, int column, ExcelCell cell)
    {
        var reference = ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
        var style = cell.Style ?? DefaultStyle(cell.Kind);
        writer.WriteStartElement("c", SpreadsheetNs);
        writer.WriteAttributeString("r", reference);
        if (style > 0) writer.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));

        if (cell.Value is null)
        {
            writer.WriteEndElement();
            return;
        }

        if (cell.Kind is CellKind.Text or CellKind.WrappedText)
        {
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", SpreadsheetNs);
            writer.WriteStartElement("t", SpreadsheetNs);
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteString(Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        else
        {
            var value = cell.Kind == CellKind.Date
                ? Convert.ToDateTime(cell.Value, CultureInfo.InvariantCulture).ToOADate().ToString("0.############", CultureInfo.InvariantCulture)
                : Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            writer.WriteElementString("v", SpreadsheetNs, value);
        }
        writer.WriteEndElement();
    }

    private static int DefaultStyle(CellKind kind) => kind switch
    {
        CellKind.Date => 6,
        CellKind.Number => 8,
        CellKind.Integer => 21,
        CellKind.Percentage => 10,
        CellKind.WrappedText => 19,
        _ => 4
    };

    private static string ColumnName(int number)
    {
        var result = string.Empty;
        while (number > 0)
        {
            number--;
            result = (char)('A' + number % 26) + result;
            number /= 26;
        }
        return result;
    }

    private static void WriteMergeCells(XmlWriter writer, params string[] ranges)
    {
        writer.WriteStartElement("mergeCells", SpreadsheetNs);
        writer.WriteAttributeString("count", ranges.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var range in ranges)
        {
            writer.WriteStartElement("mergeCell", SpreadsheetNs);
            writer.WriteAttributeString("ref", range);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WritePageSettings(XmlWriter writer)
    {
        writer.WriteStartElement("pageMargins", SpreadsheetNs);
        writer.WriteAttributeString("left", "0.35");
        writer.WriteAttributeString("right", "0.35");
        writer.WriteAttributeString("top", "0.55");
        writer.WriteAttributeString("bottom", "0.55");
        writer.WriteAttributeString("header", "0.2");
        writer.WriteAttributeString("footer", "0.2");
        writer.WriteEndElement();
        writer.WriteStartElement("pageSetup", SpreadsheetNs);
        writer.WriteAttributeString("orientation", "landscape");
        writer.WriteAttributeString("fitToWidth", "1");
        writer.WriteAttributeString("fitToHeight", "0");
        writer.WriteEndElement();
    }

    private static void WriteXmlEntry(ZipArchive archive, string path, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false });
        write(writer);
    }

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ContentTypes(int sheetCount)
    {
        var sheets = string.Concat(Enumerable.Range(1, sheetCount).Select(index => $"<Override PartName=\"/xl/worksheets/sheet{index}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>{sheets}<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/></Types>";
    }

    private static string RootRelationships() => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/></Relationships>";

    private static string CoreProperties(DateTime generatedAtUtc) => $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><dc:title>Reporte NetWatch</dc:title><dc:creator>NetWatch NMS</dc:creator><cp:lastModifiedBy>NetWatch NMS</cp:lastModifiedBy><dcterms:created xsi:type=\"dcterms:W3CDTF\">{generatedAtUtc:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">{generatedAtUtc:yyyy-MM-ddTHH:mm:ssZ}</dcterms:modified></cp:coreProperties>";
    private static string AppProperties() => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>NetWatch NMS</Application><AppVersion>1.0</AppVersion></Properties>";

    private static string Workbook(IReadOnlyList<DataSheet> sheets)
    {
        var sheetNodes = new StringBuilder("<sheet name=\"Resumen\" sheetId=\"1\" r:id=\"rId1\"/>");
        for (var index = 0; index < sheets.Count; index++) sheetNodes.Append($"<sheet name=\"{sheets[index].Name}\" sheetId=\"{index + 2}\" r:id=\"rId{index + 2}\"/>");
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{SpreadsheetNs}\" xmlns:r=\"{RelationshipsNs}\"><bookViews><workbookView activeTab=\"0\"/></bookViews><sheets>{sheetNodes}</sheets><calcPr calcId=\"191029\" fullCalcOnLoad=\"1\"/></workbook>";
    }

    private static string WorkbookRelationships(int sheetCount)
    {
        var relationships = new StringBuilder();
        for (var index = 1; index <= sheetCount; index++) relationships.Append($"<Relationship Id=\"rId{index}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{index}.xml\"/>");
        relationships.Append($"<Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{relationships}</Relationships>";
    }

    private static string Styles() => """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <numFmts count="2"><numFmt numFmtId="164" formatCode="dd/mm/yyyy hh:mm:ss"/><numFmt numFmtId="165" formatCode="0.00%"/></numFmts>
  <fonts count="6">
    <font><sz val="10"/><color rgb="FF1F2937"/><name val="Aptos"/><family val="2"/></font>
    <font><b/><sz val="16"/><color rgb="FFFFFFFF"/><name val="Aptos Display"/><family val="2"/></font>
    <font><sz val="10"/><color rgb="FFE2EEF7"/><name val="Aptos"/><family val="2"/></font>
    <font><b/><sz val="10"/><color rgb="FFFFFFFF"/><name val="Aptos"/><family val="2"/></font>
    <font><b/><sz val="10"/><color rgb="FF172234"/><name val="Aptos"/><family val="2"/></font>
    <font><b/><sz val="10"/><color rgb="FF9F2632"/><name val="Aptos"/><family val="2"/></font>
  </fonts>
  <fills count="10">
    <fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF0B1F33"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF0F766E"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF2563EB"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFF4F7FB"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFE8F1FF"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFE6F6EF"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFFFF3D6"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFFDE8EA"/><bgColor indexed="64"/></patternFill></fill>
  </fills>
  <borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left/><right/><top/><bottom style="thin"><color rgb="FFDCE5EF"/></bottom><diagonal/></border></borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="25">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment vertical="center"/></xf>
    <xf numFmtId="0" fontId="2" fillId="3" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment vertical="center"/></xf>
    <xf numFmtId="0" fontId="3" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf>
    <xf numFmtId="0" fontId="0" fillId="5" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf>
    <xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf>
    <xf numFmtId="164" fontId="0" fillId="5" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf>
    <xf numFmtId="2" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
    <xf numFmtId="2" fontId="0" fillId="5" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
    <xf numFmtId="165" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
    <xf numFmtId="165" fontId="0" fillId="5" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
    <xf numFmtId="165" fontId="4" fillId="8" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/><xf numFmtId="165" fontId="5" fillId="9" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="2" fontId="4" fillId="8" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/><xf numFmtId="2" fontId="5" fillId="9" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="0" fontId="3" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="4" fillId="6" borderId="1" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="4" fillId="7" borderId="1" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment vertical="center"/></xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf><xf numFmtId="0" fontId="0" fillId="5" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>
    <xf numFmtId="1" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1"/><xf numFmtId="1" fontId="0" fillId="5" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="0" fontId="5" fillId="9" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/><xf numFmtId="0" fontId="4" fillId="8" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>
  </cellXfs>
  <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
""";
}
