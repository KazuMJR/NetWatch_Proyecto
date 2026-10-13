using System.Globalization;
using System.Text;
using NetWatch.API.Contracts;

namespace NetWatch.API.Services;

public static class PdfReportBuilder
{
    private const double PageWidth = 612;
    private const double PageHeight = 792;
    private const double Left = 36;
    private const double TableWidth = 540;
    private const double BodyBottom = 738;

    public static byte[] Build(
        IReadOnlyList<ReportRowDto> rows,
        string type,
        string deviceLabel,
        DateTime fromUtc,
        DateTime toUtc,
        DateTime generatedAtUtc)
    {
        var metricLayout = string.Equals(type, "metrics", StringComparison.OrdinalIgnoreCase);
        var pages = new List<PdfCanvas>();
        var page = NewPage(type, deviceLabel, fromUtc, toUtc, rows.Count);
        pages.Add(page);
        var top = DrawTableHeader(page, metricLayout);

        foreach (var row in rows)
        {
            var height = metricLayout ? 25d : GenericRowHeight(row.Detail);
            if (top + height > BodyBottom)
            {
                page = NewPage(type, deviceLabel, fromUtc, toUtc, rows.Count);
                pages.Add(page);
                top = DrawTableHeader(page, metricLayout);
            }

            if (metricLayout) DrawMetricRow(page, row, top, height, (pages.Count + (int)top) % 2 == 0);
            else DrawGenericRow(page, row, top, height, (pages.Count + (int)top) % 2 == 0);
            top += height;
        }

        if (rows.Count == 0)
        {
            page.FillRect(Left, top, TableWidth, 58, "F8FAFC");
            page.Text(Left + TableWidth / 2, top + 24, "No se encontraron registros para los filtros seleccionados.", 10, true, "66768A", TextAlign.Center);
        }

        for (var index = 0; index < pages.Count; index++)
        {
            var footer = $"Documento listo para impresión  |  Generado {generatedAtUtc:dd/MM/yyyy HH:mm} UTC  |  Página {index + 1} de {pages.Count}";
            pages[index].Line(Left, 754, Left + TableWidth, 754, "D9E2EC", .7);
            pages[index].Text(Left + TableWidth / 2, 764, footer, 7.5, false, "66768A", TextAlign.Center);
        }

        return SimplePdfWriter.Write(pages.Select(x => x.Content).ToList(), "Reporte NetWatch NMS", generatedAtUtc);
    }

    private static PdfCanvas NewPage(string type, string deviceLabel, DateTime fromUtc, DateTime toUtc, int count)
    {
        var page = new PdfCanvas(PageHeight);
        page.FillRect(0, 0, PageWidth, 72, "0B1F35");
        page.FillRect(0, 69, PageWidth, 3, "19B8C8");
        page.Text(Left, 22, "NetWatch NMS", 18, true, "FFFFFF");
        page.Text(Left, 46, $"Reporte de {TypeLabel(type)}", 10, false, "C7D8E8");
        page.Text(Left + TableWidth, 24, "REPORTE OPERATIVO", 8.5, true, "7DE3F2", TextAlign.Right);
        page.Text(Left + TableWidth, 45, $"{count:N0} registros", 9, false, "D9E7F2", TextAlign.Right);

        page.FillRect(Left, 86, TableWidth, 54, "F4F7FB");
        page.StrokeRect(Left, 86, TableWidth, 54, "DCE5EF", .8);
        page.Text(Left + 14, 101, "DISPOSITIVO", 6.8, true, "6B7B8F");
        page.Text(Left + 14, 117, deviceLabel, 9.2, true, "172234");
        page.Text(Left + 205, 101, "PERIODO UTC", 6.8, true, "6B7B8F");
        page.Text(Left + 205, 117, $"{fromUtc:dd/MM/yyyy} - {toUtc:dd/MM/yyyy}", 9.2, true, "172234");
        page.Text(Left + 422, 101, "FORMATO", 6.8, true, "6B7B8F");
        page.Text(Left + 422, 117, "Carta vertical", 9.2, true, "172234");
        return page;
    }

    private static double DrawTableHeader(PdfCanvas page, bool metrics)
    {
        const double top = 154;
        const double height = 28;
        page.FillRect(Left, top, TableWidth, height, "2563EB");
        var columns = metrics
            ? new[] { ("FECHA", 76d), ("DISPOSITIVO", 105d), ("CPU", 44d), ("RAM", 44d), ("DISCO", 44d), ("TEMP.", 55d), ("TRÁFICO", 82d), ("RESP.", 90d) }
            : new[] { ("FECHA UTC", 80d), ("DISPOSITIVO", 105d), ("CATEGORÍA", 70d), ("DETALLE", 285d) };
        var x = Left;
        foreach (var column in columns)
        {
            page.Text(x + 7, top + 10, column.Item1, 7.2, true, "FFFFFF");
            x += column.Item2;
        }
        return top + height;
    }

    private static void DrawMetricRow(PdfCanvas page, ReportRowDto row, double top, double height, bool alternate)
    {
        page.FillRect(Left, top, TableWidth, height, alternate ? "F6F9FC" : "FFFFFF");
        page.Line(Left, top + height, Left + TableWidth, top + height, "E1E8F0", .55);
        var values = new[]
        {
            row.DateUtc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            row.DeviceName,
            Percent(row.CpuPercent),
            Percent(row.MemoryPercent),
            Percent(row.DiskPercent),
            Value(row.TemperatureCelsius, " °C"),
            Value(row.NetworkTrafficMbps, " Mbps"),
            Value(row.ResponseTimeMs, " ms")
        };
        var widths = new[] { 76d, 105d, 44d, 44d, 44d, 55d, 82d, 90d };
        var x = Left;
        for (var index = 0; index < values.Length; index++)
        {
            var align = index >= 2 ? TextAlign.Right : TextAlign.Left;
            var textX = align == TextAlign.Right ? x + widths[index] - 7 : x + 7;
            page.Text(textX, top + 9, Truncate(values[index], widths[index], 6.8), 6.8, index == 1, index is 2 or 3 or 4 ? "1748BC" : "26364B", align);
            x += widths[index];
        }
    }

    private static void DrawGenericRow(PdfCanvas page, ReportRowDto row, double top, double height, bool alternate)
    {
        page.FillRect(Left, top, TableWidth, height, alternate ? "F6F9FC" : "FFFFFF");
        page.Line(Left, top + height, Left + TableWidth, top + height, "E1E8F0", .55);
        page.Text(Left + 7, top + 9, row.DateUtc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), 7.4, false, "26364B");
        page.Text(Left + 87, top + 9, Truncate(row.DeviceName, 91, 7.4), 7.4, true, "172234");
        var categoryColor = row.Category switch { "Alerta" => "B4232E", "Evento" => "9A6200", "Estado" => "4D6076", _ => "087A64" };
        page.Text(Left + 192, top + 9, row.Category, 7.4, true, categoryColor);
        var lines = Wrap(row.Detail.Replace(" | ", " - "), 269, 7.4, 4);
        for (var index = 0; index < lines.Count; index++)
            page.Text(Left + 262, top + 8 + index * 10, lines[index], 7.4, false, "405065");
    }

    private static double GenericRowHeight(string detail) => Math.Max(25, 14 + Wrap(detail.Replace(" | ", " - "), 269, 7.4, 4).Count * 10);
    private static string Percent(decimal? value) => value.HasValue ? $"{value:0.##}%" : "-";
    private static string Value(decimal? value, string suffix) => value.HasValue ? $"{value:0.##}{suffix}" : "-";
    private static string TypeLabel(string type) => type.ToLowerInvariant() switch
    {
        "all" => "todos los datos",
        "events" => "eventos del sistema",
        "alerts" => "alertas generadas",
        "states" => "cambios de estado",
        _ => "métricas de rendimiento"
    };

    private static string Truncate(string value, double width, double fontSize)
    {
        var capacity = Math.Max(3, (int)(width / (fontSize * .53)));
        return value.Length <= capacity ? value : value[..Math.Max(1, capacity - 3)] + "...";
    }

    private static IReadOnlyList<string> Wrap(string value, double width, double fontSize, int maxLines)
    {
        var capacity = Math.Max(8, (int)(width / (fontSize * .52)));
        var words = Clean(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > capacity)
            {
                lines.Add(current.ToString());
                current.Clear();
                if (lines.Count == maxLines) break;
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word.Length > capacity ? word[..capacity] : word);
        }
        if (lines.Count < maxLines && current.Length > 0) lines.Add(current.ToString());
        if (lines.Count == maxLines && words.Length > 0 && !string.Join(' ', lines).EndsWith(words[^1], StringComparison.Ordinal))
            lines[^1] = Truncate(lines[^1] + "...", width, fontSize);
        return lines.Count == 0 ? [string.Empty] : lines;
    }

    private static string Clean(string? value) => (value ?? string.Empty)
        .Replace('→', '>')
        .Replace('–', '-')
        .Replace('—', '-')
        .Replace('“', '"')
        .Replace('”', '"')
        .Replace('’', '\'');

    private enum TextAlign { Left, Center, Right }

    private sealed class PdfCanvas(double height)
    {
        private readonly StringBuilder commands = new();
        public string Content => commands.ToString();

        public void FillRect(double x, double top, double rectWidth, double rectHeight, string color)
        {
            commands.AppendLine($"{Rgb(color)} rg {F(x)} {F(height - top - rectHeight)} {F(rectWidth)} {F(rectHeight)} re f");
        }

        public void StrokeRect(double x, double top, double rectWidth, double rectHeight, string color, double lineWidth)
        {
            commands.AppendLine($"{Rgb(color)} RG {F(lineWidth)} w {F(x)} {F(height - top - rectHeight)} {F(rectWidth)} {F(rectHeight)} re S");
        }

        public void Line(double x1, double top1, double x2, double top2, string color, double lineWidth)
        {
            commands.AppendLine($"{Rgb(color)} RG {F(lineWidth)} w {F(x1)} {F(height - top1)} m {F(x2)} {F(height - top2)} l S");
        }

        public void Text(double x, double top, string value, double size, bool bold, string color, TextAlign align = TextAlign.Left)
        {
            var clean = Clean(value);
            var estimatedWidth = clean.Length * size * .51;
            var positionedX = align switch { TextAlign.Center => x - estimatedWidth / 2, TextAlign.Right => x - estimatedWidth, _ => x };
            commands.AppendLine($"BT /{(bold ? "F2" : "F1")} {F(size)} Tf {Rgb(color)} rg {F(positionedX)} {F(height - top - size)} Td ({Escape(clean)}) Tj ET");
        }

        public static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        private static string Rgb(string hex)
        {
            var r = int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            var g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            var b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return $"{F(r)} {F(g)} {F(b)}";
        }
        private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static class SimplePdfWriter
    {
        public static byte[] Write(IReadOnlyList<string> contents, string title, DateTime generatedAtUtc)
        {
            var pageCount = contents.Count;
            var infoId = 5 + pageCount * 2;
            var objects = new Dictionary<int, byte[]>();
            objects[1] = Bytes("<< /Type /Catalog /Pages 2 0 R >>");
            var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(index => $"{5 + index * 2} 0 R"));
            objects[2] = Bytes($"<< /Type /Pages /Count {pageCount} /Kids [{kids}] >>");
            objects[3] = Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            objects[4] = Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

            for (var index = 0; index < pageCount; index++)
            {
                var pageId = 5 + index * 2;
                var contentId = pageId + 1;
                objects[pageId] = Bytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0} {PageHeight:0}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentId} 0 R >>");
                var stream = Bytes(contents[index]);
                using var objectStream = new MemoryStream();
                Write(objectStream, $"<< /Length {stream.Length} >>\nstream\n");
                objectStream.Write(stream);
                Write(objectStream, "\nendstream");
                objects[contentId] = objectStream.ToArray();
            }

            objects[infoId] = Bytes($"<< /Title ({PdfCanvas.Escape(Clean(title))}) /Author (NetWatch NMS) /Creator (NetWatch NMS) /CreationDate (D:{generatedAtUtc:yyyyMMddHHmmss}Z) >>");

            using var pdf = new MemoryStream();
            Write(pdf, "%PDF-1.4\n%âãÏÓ\n");
            var offsets = new long[infoId + 1];
            for (var id = 1; id <= infoId; id++)
            {
                offsets[id] = pdf.Position;
                Write(pdf, $"{id} 0 obj\n");
                pdf.Write(objects[id]);
                Write(pdf, "\nendobj\n");
            }
            var xref = pdf.Position;
            Write(pdf, $"xref\n0 {infoId + 1}\n0000000000 65535 f \n");
            for (var id = 1; id <= infoId; id++) Write(pdf, $"{offsets[id]:0000000000} 00000 n \n");
            Write(pdf, $"trailer\n<< /Size {infoId + 1} /Root 1 0 R /Info {infoId} 0 R >>\nstartxref\n{xref}\n%%EOF");
            return pdf.ToArray();
        }

        private static byte[] Bytes(string value) => Encoding.Latin1.GetBytes(Clean(value));
        private static void Write(Stream stream, string value) => stream.Write(Bytes(value));
    }
}
