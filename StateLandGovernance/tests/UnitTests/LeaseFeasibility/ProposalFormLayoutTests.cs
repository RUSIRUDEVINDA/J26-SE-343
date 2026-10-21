using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Proposals;

namespace StateLandGovernance.UnitTests.LeaseFeasibility;

public sealed class ProposalFormLayoutTests
{
    private static string Draft(string name = "Example applicant") => "Test project\n" + string.Join("\n", Enumerable.Range(1, 14).Select(n => $"{n:00}. Section\n" + (n == 1 ? $"Applicant name: {name}\nAddress: [TO BE CONFIRMED]" : n == 2 ? "Project: Edited project\nProject introduction and objectives: Applicant project overview" : n == 3 ? "Required materials and sourcing methods: 01 | Seedlings | Local supplier" : n == 11 ? "Monthly net profit: LKR 20,000.00\nAnnual net profit (12 equal months): LKR 240,000.00" : "")));
    [Fact]
    public void BlankFormHasElevenPagesThreeDiagramBoxesAndAllFinancialTables()
    {
        var pages = LandDepartmentProposalForm.Build("", true);
        Assert.Equal(11, pages.Count);
        Assert.Equal(3, pages.SelectMany(p => p.Blocks).Count(b => b.Kind == "box"));
        Assert.Contains(pages[4].Blocks, b => b.Kind == "table" && b.Headers!.Length == 6);
        Assert.Contains(pages[8].Blocks, b => b.Kind == "table" && b.Headers!.Length == 3 && b.Rows!.Length == 31);
        Assert.Contains(pages[9].Blocks, b => b.Kind == "table" && b.Headers!.Length == 14);
        Assert.Contains(pages[10].Blocks, b => b.Text.Contains("අත්සන"));
    }
    [Fact]
    public void WordContainsActualTablesBlankCellsPageBreaksAndDiagramHeights()
    {
        using var zip = new ZipArchive(new MemoryStream(ProposalFormRenderer.Word(LandDepartmentProposalForm.Build("", true), "Sinhala MN")));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var xml = XDocument.Load(stream); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        Assert.True(xml.Descendants(w + "tbl").Count() >= 10);
        Assert.Equal(10, xml.Descendants(w + "pageBreakBefore").Count());
        Assert.Contains(xml.Descendants(w + "trHeight"), e => (string?)e.Attribute(w + "val") == "4800");
        Assert.NotNull(zip.GetEntry("word/footer1.xml"));
    }
    [Fact]
    public void ParsesEditedValuesAndLeavesUnknownAnswersBlank()
    {
        var fields = LandDepartmentProposalForm.Parse(Draft());
        Assert.Equal("Example applicant", fields["applicantName"]);
        Assert.Equal("", fields["address"]);
        Assert.Equal("Edited project", fields["projectName"]);
        var blocks = LandDepartmentProposalForm.Build(Draft()).SelectMany(p => p.Blocks);
        Assert.Contains(blocks, b => b.Kind == "table" && b.Rows!.Any(r => r.Contains("Seedlings")));
    }
    [Fact]
    public void HtmlEncodesApplicantContentAndSharesTemplatePages()
    {
        var html = ProposalFormRenderer.Html(LandDepartmentProposalForm.Build(Draft("<script>alert(1)</script>")));
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
        Assert.Equal(11, html.Split("<section class=\"page\">").Length - 1);
        Assert.Contains("Print / Save PDF", html);
    }
    [Fact]
    public void RejectsUnstructuredTextInsteadOfSilentlyDroppingEdits()
    {
        Assert.Throws<ArgumentException>(() => LandDepartmentProposalForm.Build("some free text"));
    }
    [Fact]
    public void KeepsAdditionalNarrativesInAnAnnexure()
    {
        var pages = LandDepartmentProposalForm.Build(Draft() + "\nAI narrative suggestion - requires verification:\nKeep this extra narrative.\n");
        Assert.Equal(12, pages.Count);
        Assert.Contains(pages.Last().Blocks, b => b.Text.Contains("Keep this extra narrative."));
    }
}
