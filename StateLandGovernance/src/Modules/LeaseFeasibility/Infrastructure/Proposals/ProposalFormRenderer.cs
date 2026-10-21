using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Proposals;

public static class ProposalFormRenderer
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static byte[] Word(IReadOnlyList<ProposalFormPage> pages, string font)
    {
        var body = new XElement(W + "body");
        foreach (var page in pages)
        {
            var first = true;
            foreach (var block in page.Blocks)
            {
                var nodes = RenderBlock(block, font).ToArray();
                if (first && page.Number > 1)
                {
                    // The first block on every template page is a heading paragraph.
                    nodes[0].Element(W + "pPr")!.Add(new XElement(W + "pageBreakBefore"));
                }
                foreach (var node in nodes) body.Add(node);
                first = false;
            }
        }
        body.Add(new XElement(W + "sectPr",
            new XElement(W + "footerReference", new XAttribute(W + "type", "default"), new XAttribute(XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships") + "id", "rIdFooter")),
            new XElement(W + "pgSz", new XAttribute(W + "w", "11906"), new XAttribute(W + "h", "16838")),
            new XElement(W + "pgMar", new XAttribute(W + "top", "1000"), new XAttribute(W + "bottom", "1000"), new XAttribute(W + "left", "1134"), new XAttribute(W + "right", "1134"), new XAttribute(W + "footer", "450"))));
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Write("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/footer1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/></Types>");
            Write("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Write("word/_rels/document.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rIdFooter\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer\" Target=\"footer1.xml\"/></Relationships>");
            Write("word/footer1.xml", new XElement(W + "ftr", new XAttribute(XNamespace.Xmlns + "w", W),
                new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "jc", new XAttribute(W + "val", "center"))),
                    new XElement(W + "fldSimple", new XAttribute(W + "instr", "PAGE"), new XElement(W + "r", new XElement(W + "t", "1"))))).ToString());
            Write("word/document.xml", new XDocument(new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), body)).ToString());
            void Write(string name, string value) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(value); }
        }
        return stream.ToArray();
    }
    private static IEnumerable<XElement> RenderBlock(ProposalFormBlock block, string font)
    {
        switch (block.Kind)
        {
            case "heading": yield return Paragraph(block.Text, font, bold: true, underline: true, before: 110, after: 90, keep: true); break;
            case "paragraph": yield return Paragraph(block.Text, font); break;
            case "field":
                yield return Table(new[] { new[] { block.Text, ":", block.Rows![0][0] } }, new double[] { 43,3,54 }, font, 24, false); break;
            case "lines":
                if (block.Text != "") yield return Paragraph(block.Text, font, keep: true);
                var value = block.Rows![0][0];
                if (value != "") yield return Paragraph(value, font);
                for (var i = 0; i < (value == "" ? block.Height : 1); i++)
                    {
                    var line = Paragraph("", font, size: 20, after: 35);
                    line.Element(W + "pPr")!.Add(new XElement(W + "pBdr", new[] { "bottom", "between" }.Select(side => new XElement(W + side, new XAttribute(W + "val", "dotted"), new XAttribute(W + "sz", "4"), new XAttribute(W + "space", "1")))));
                    yield return line;
                }
                break;
            case "box":
                yield return Paragraph(block.Text, font, size: 19, keep: true);
                yield return Table(new[] { new[] { block.Rows![0][0] } }, new double[] { 100 }, font, block.Height, true);
                yield return Paragraph("", font, size: 10, after: 60);
                break;
            case "table":
                var rows = block.Headers!.Any(h => h != "") ? new[] { block.Headers! }.Concat(block.Rows!).ToArray() : block.Rows!;
                yield return Table(rows, block.Widths!, font, block.Height, block.Border, block.Headers!.Any(h => h != ""));
                yield return Paragraph("", font, size: 8, after: 20);
                break;
            default: throw new InvalidOperationException("Unknown proposal layout block.");
        }
    }
    private static XElement Paragraph(string text, string font, int size = 21, bool bold = false, bool underline = false,
        int before = 0, int after = 55, bool keep = false, bool compact = false)
    {
        var properties = new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "before", before), new XAttribute(W + "after", after), new XAttribute(W + "line", compact ? "220" : "270"), new XAttribute(W + "lineRule", compact ? "atLeast" : "auto")));
        if (keep) properties.Add(new XElement(W + "keepNext"));
        var runProperties = new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", font), new XAttribute(W + "hAnsi", font), new XAttribute(W + "cs", font)),
            new XElement(W + "sz", new XAttribute(W + "val", size)), new XElement(W + "szCs", new XAttribute(W + "val", size)));
        if (bold) { runProperties.Add(new XElement(W + "b")); runProperties.Add(new XElement(W + "bCs")); }
        if (underline) runProperties.Add(new XElement(W + "u", new XAttribute(W + "val", "single")));
        var run = new XElement(W + "r", runProperties);
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++) { if (i > 0) run.Add(new XElement(W + "br")); run.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), lines[i])); }
        return new XElement(W + "p", properties, run);
    }
    private static XElement Table(string[][] rows, double[] widths, string font, int height, bool border, bool header = false)
    {
        var tableProperties = new XElement(W + "tblPr", new XElement(W + "tblW", new XAttribute(W + "w", "9638"), new XAttribute(W + "type", "dxa")),
            new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed")),
            new XElement(W + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(side => new XElement(W + side, new XAttribute(W + "w", "45"), new XAttribute(W + "type", "dxa")))));
        tableProperties.Add(new XElement(W + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(side =>
            new XElement(W + side, new XAttribute(W + "val", border ? "single" : "nil"), new XAttribute(W + "sz", "6")))));
        var table = new XElement(W + "tbl", tableProperties, new XElement(W + "tblGrid", widths.Select(width => new XElement(W + "gridCol", new XAttribute(W + "w", (int)(9638 * width / 100))))));
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var rowProperties = new XElement(W + "trPr", new XElement(W + "trHeight", new XAttribute(W + "val", height * 20), new XAttribute(W + "hRule", "atLeast")), new XElement(W + "cantSplit"));
            if (header && rowIndex == 0) rowProperties.Add(new XElement(W + "tblHeader"));
            var row = new XElement(W + "tr", rowProperties);
            for (var column = 0; column < widths.Length; column++)
            {
                var size = widths.Length >= 10 ? 15 : widths.Length >= 6 ? 17 : 20;
                var cell = new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", (int)(9638 * widths[column] / 100)), new XAttribute(W + "type", "dxa")), new XElement(W + "vAlign", new XAttribute(W + "val", "center"))),
                    Paragraph(rows[rowIndex][column] ?? "", font, size, bold: header && rowIndex == 0, after: 0, compact: true));
                row.Add(cell);
            }
            table.Add(row);
        }
        return table;
    }
    public static string Html(IReadOnlyList<ProposalFormPage> pages)
    {
        string E(string? value) => WebUtility.HtmlEncode(value ?? "").Replace("\n", "<br>");
        var html = new StringBuilder("<!doctype html><html lang=\"si\"><head><meta charset=\"utf-8\"><title>Land Department proposal form</title><style>");
        html.Append("@page{size:A4;margin:17.6mm 20mm}*{box-sizing:border-box}body{margin:0;background:#ddd;font:10.5pt 'Sinhala MN','Noto Sans Sinhala','Nirmala UI',sans-serif;color:#000}.page{width:170mm;min-height:261mm;margin:12mm auto;background:#fff;padding:0;break-after:page}.page:last-child{break-after:auto}h2{font-size:10.5pt;text-decoration:underline;margin:5pt 0 5pt;break-after:avoid}p{margin:0 0 4pt;line-height:1.2;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse;table-layout:fixed;margin:0 0 3pt}th,td{border:1px solid #000;padding:2pt;overflow-wrap:anywhere;font-weight:normal;vertical-align:middle}th{font-weight:bold}thead{display:table-header-group}tr{break-inside:avoid}.plain td{border:0}.field td{border:0;padding:2pt 0}.line{height:16pt;border-bottom:1px dotted #555;margin-bottom:2pt}.box{border:1px solid #000;padding:6pt;overflow-wrap:anywhere;margin:4pt 0 9pt}.page-number{text-align:center;font-size:9pt;margin:8pt}.tools{position:sticky;top:0;background:#fff;padding:12px;text-align:center}.tools button{font:inherit;padding:8px 18px}@media print{body{background:#fff}.page{margin:0;min-height:0;width:auto}.tools{display:none}} ");
        html.Append("</style></head><body><div class=\"tools\"><button onclick=\"window.print()\">Print / Save PDF</button></div>");
        foreach (var page in pages)
        {
            html.Append("<section class=\"page\">");
            foreach (var block in page.Blocks)
            {
                switch (block.Kind)
                {
                    case "heading": html.Append("<h2>").Append(E(block.Text)).Append("</h2>"); break;
                    case "paragraph": html.Append("<p>").Append(E(block.Text)).Append("</p>"); break;
                    case "field": html.Append("<table class=\"field\"><colgroup><col style=\"width:43%\"><col style=\"width:3%\"><col style=\"width:54%\"></colgroup><tr><td>").Append(E(block.Text)).Append("</td><td>:</td><td>").Append(E(block.Rows![0][0])).Append("</td></tr></table>"); break;
                    case "lines":
                        if (block.Text != "") html.Append("<p>").Append(E(block.Text)).Append("</p>");
                        var value = block.Rows![0][0];
                        if (value != "") html.Append("<p>").Append(E(value)).Append("</p>");
                        for (var i = 0; i < (value == "" ? block.Height : 1); i++) html.Append("<div class=\"line\"></div>");
                        break;
                    case "box": html.Append("<p>").Append(E(block.Text)).Append("</p><div class=\"box\" style=\"min-height:").Append(block.Height).Append("pt\">").Append(E(block.Rows![0][0])).Append("</div>"); break;
                    case "table":
                        var fontSize = block.Widths!.Length >= 10 ? "7.5" : block.Widths.Length >= 6 ? "8.5" : "10";
                        html.Append("<table class=\"").Append(block.Border ? "grid" : "plain").Append("\" style=\"font-size:").Append(fontSize).Append("pt\"><colgroup>");
                        foreach (var width in block.Widths) html.Append("<col style=\"width:").Append(width.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("%\">");
                        html.Append("</colgroup>");
                        if (block.Headers!.Any(h => h != "")) { html.Append("<thead><tr>"); foreach (var value2 in block.Headers!) html.Append("<th>").Append(E(value2)).Append("</th>"); html.Append("</tr></thead>"); }
                        html.Append("<tbody>");
                        foreach (var row in block.Rows!) { html.Append("<tr style=\"height:").Append(block.Height).Append("pt\">"); foreach (var cell in row) html.Append("<td>").Append(E(cell)).Append("</td>"); html.Append("</tr>"); }
                        html.Append("</tbody></table>"); break;
                }
            }
            html.Append("<p class=\"page-number\">").Append(page.Number).Append("</p></section>");
        }
        return html.Append("</body></html>").ToString();
    }
}
