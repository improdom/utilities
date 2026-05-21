using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.EnableDebugging = true;

var outputPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "PortfolioOverview-CST.pdf");
var model = CstReportData.Create();

Document.Create(container =>
{
    container.Page(page =>
    {
        // Very tall page to avoid internal pagination — limited by Skia max height
        // Use 14,400 points (200 inches) which is the observed Skia maximum.
        page.Size(new PageSize(PageSizes.A4.Landscape().Width, 14400));
        // Minimal margins to maximize usable height
        page.MarginLeft(0);
        page.MarginRight(0);
        page.MarginTop(0);
        page.MarginBottom(0);
        page.PageColor(Colors.White);
        // Smaller default text to compact the table vertically
        page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(4.0f).FontColor(CstColors.Text));
        page.Content().Element(c => ComposePage(c, model));
    });
}).GeneratePdf(outputPath);

Console.WriteLine($"Created: {outputPath}");

static void ComposePage(IContainer container, CstReportModel model)
{
    container.Column(col =>
    {
        col.Spacing(1);
        col.Item().Text("Portfolio Overview").FontSize(14).Bold().FontColor(CstColors.Text);
        col.Item().TranslateY(-3).Text("All figures are in USDm unless otherwise stated").FontSize(7.2f).FontColor(CstColors.Subtitle);
        col.Item().Height(14);
        col.Item().Text("Table 1 Combined Stress Testing (CST)").FontSize(8).Bold().FontColor(CstColors.Text);
        col.Item().Height(8);
        col.Item().Table(table => BuildMainTable(table, model));
        col.Item().Height(4);
        col.Item().Element(ComposeNotes);
    });
}

static void BuildMainTable(TableDescriptor table, CstReportModel model)
{
    table.ColumnsDefinition(columns =>
    {
        columns.ConstantColumn(126);
        columns.ConstantColumn(138);
        for (var i = 0; i < 12; i++) columns.ConstantColumn(39);
        columns.ConstantColumn(58);
    });

    table.Header(header =>
    {
        header.Cell().ColumnSpan(2).Element(TopBlank);
        header.Cell().ColumnSpan(5).Element(SuperHeader).AlignCenter().Text("Official Feb-26").FontSize(5.3f);
        header.Cell().ColumnSpan(5).Element(SuperHeader).AlignCenter().Text("Estimate¹").FontSize(5.3f);
        header.Cell().ColumnSpan(2).Element(SuperHeader).AlignCenter().Text("Difference vs. Official").FontSize(5.3f);
        header.Cell().Element(TopBlank);

        header.Cell().ColumnSpan(2).Element(SecondBlank);
        foreach (var h in new[] { "IB", "Group\nTreasury", "NCL", "Others", "Group Total", "IB", "Group\nTreasury", "NCL", "Others", "Group Total", "Group Total", "", "Update frequency" })
            header.Cell().Element(HeaderCell).AlignCenter().Text(h).FontSize(5.2f).SemiBold();
    });

    string? lastGroup = null;
    foreach (var r in model.Rows)
    {
        var isNewGroup = r.Group != lastGroup;
        lastGroup = r.Group;

        table.Cell().Element(c => GroupCell(c, isNewGroup, r.IsSectionDivider, r.GroupTone)).Text(isNewGroup ? r.Group : "").FontSize(5.2f).Bold();
        table.Cell().Element(c => LabelCell(c, r.IsSectionDivider, r.IsTotal)).Text(r.Label).FontSize(5.2f).SemiBold(r.IsTotal);

        for (var i = 0; i < r.Official.Length; i++)
            table.Cell().Element(c => NumberCell(c, r.IsSectionDivider, r.IsTotal, i == 4)).Text(r.Official[i]).FontSize(5.15f).SemiBold(r.IsTotal).AlignRight();

        for (var i = 0; i < r.Estimate.Length; i++)
            table.Cell().Element(c => NumberCell(c, r.IsSectionDivider, r.IsTotal, i == 4)).Text(r.Estimate[i]).FontSize(5.15f).SemiBold(r.IsTotal).AlignRight();

        table.Cell().Element(c => NumberCell(c, r.IsSectionDivider, r.IsTotal, false)).Text(r.Difference).FontSize(5.15f).SemiBold(r.IsTotal).AlignRight();
        table.Cell().Element(c => NumberCell(c, r.IsSectionDivider, r.IsTotal, false)).Text(r.DifferenceNote).FontSize(5.15f).SemiBold(r.IsTotal).AlignCenter();
        table.Cell().Element(c => UpdateCell(c, r.IsSectionDivider, r.IsTotal)).Text(r.UpdateFrequency).FontSize(5f).AlignCenter();
    }

    foreach (var footer in model.FooterRows)
    {
        table.Cell().ColumnSpan(2).Element(FooterLabel).Text(footer.Label).FontSize(5.7f).SemiBold();
        foreach (var v in footer.Values)
            table.Cell().Element(FooterValue).Text(v).FontSize(5.5f).Bold().AlignRight();
    }

    table.Cell().ColumnSpan(2).Element(RagLabel).Text("% of Util").FontSize(5.5f).Bold().FontColor(CstColors.Red);
    var percents = new[] { "81%", "38%", "77%", "91%", "85%", "94%", "65%", "76%", "93%", "91%", "", "", "" };
    foreach (var p in percents)
        table.Cell().Element(RagValue).Text(p).FontSize(5.5f).Bold().FontColor(CstColors.Red).AlignCenter();

    table.Cell().ColumnSpan(2).Element(RagLabel).Text("RAG").FontSize(5.5f).Bold();
    var rags = new[] { "#D7A22A", "#06744A", "#06744A", "#B0452E", "#D7A22A", "#D7A22A", "#06744A", "#06744A", "#D7A22A", "#D7A22A", "", "", "" };
    foreach (var color in rags)
        table.Cell().Element(RagValue).AlignCenter().Text(string.IsNullOrEmpty(color) ? "" : "●").FontSize(9.2f).FontColor(string.IsNullOrEmpty(color) ? CstColors.Text : color);

    table.Cell().ColumnSpan(2).Element(BottomLabel).Text("Buffer to Trigger").FontSize(5.5f).Bold();
    var buffers = new[] { "926", "249", "302", "674", "2,150", "276", "142", "310", "585", "1,312", "", "", "" };
    foreach (var b in buffers)
        table.Cell().Element(BottomValue).Text(b).FontSize(5.5f).AlignRight();
}

static void ComposeNotes(IContainer container)
{
    container.Column(col =>
    {
        col.Spacing(2);
        col.Item().Text("Notes").FontSize(5.2f).Underline();
        col.Item().Text("1) Starting from 16 Jan 2026, Global Trade War is a new binding scenario to replace Global Crisis. Estimate table shows estimates plus intramonth updates. All other items are rolled over from the official monthly CST report.").FontSize(5.1f);
        col.Item().Text("2) Operational Risk estimate based on a revised allocation methodology, similar to the RWA approach.").FontSize(5.1f);
    });
}

static IContainer TopBlank(IContainer c) => c.Height(10).BorderBottom(0.5f).BorderColor(CstColors.Line);
static IContainer SuperHeader(IContainer c) => c.Height(10).BorderBottom(0.5f).BorderColor(CstColors.Line).AlignMiddle();
static IContainer SecondBlank(IContainer c) => c.Height(10).BorderBottom(0.5f).BorderColor(CstColors.Line);
static IContainer HeaderCell(IContainer c) => c.Height(10).BorderBottom(0.5f).BorderColor(CstColors.Line).PaddingHorizontal(2).AlignMiddle();
static IContainer GroupCell(IContainer c, bool first, bool divider, string? tone) => c.Height(8f).Background(tone ?? Colors.White).BorderBottom(divider ? 0.7f : 0.2f).BorderColor(divider ? CstColors.LineDark : CstColors.LineLight).PaddingLeft(1.5f).PaddingTop(first ? 1.2f : 0);
static IContainer LabelCell(IContainer c, bool divider, bool total) => c.Height(8f).Background(total ? CstColors.TotalFill : Colors.White).BorderBottom(divider ? 0.7f : 0.2f).BorderColor(divider ? CstColors.LineDark : CstColors.LineLight).PaddingHorizontal(2).AlignMiddle();
static IContainer NumberCell(IContainer c, bool divider, bool total, bool groupTotal) => c.Height(8f).Background(total ? CstColors.TotalFill : groupTotal ? CstColors.GroupTotalFill : Colors.White).BorderBottom(divider ? 0.7f : 0.2f).BorderColor(divider ? CstColors.LineDark : CstColors.LineLight).PaddingHorizontal(2).AlignMiddle();
static IContainer UpdateCell(IContainer c, bool divider, bool total) => c.Height(8f).Background(total ? CstColors.TotalFill : Colors.White).BorderBottom(divider ? 0.7f : 0.2f).BorderColor(divider ? CstColors.LineDark : CstColors.LineLight).PaddingHorizontal(2).AlignMiddle();
static IContainer FooterLabel(IContainer c) => c.Height(10).BorderBottom(0.35f).BorderColor(CstColors.LineLight).PaddingHorizontal(2).AlignMiddle();
static IContainer FooterValue(IContainer c) => c.Height(10).BorderBottom(0.35f).BorderColor(CstColors.LineLight).PaddingHorizontal(2).AlignMiddle();
static IContainer RagLabel(IContainer c) => c.Height(8).PaddingHorizontal(2).AlignMiddle();
static IContainer RagValue(IContainer c) => c.Height(8).PaddingHorizontal(2).AlignMiddle();
static IContainer BottomLabel(IContainer c) => c.Height(9).BorderTop(0.7f).BorderBottom(0.7f).BorderColor(CstColors.LineDark).PaddingHorizontal(2).AlignMiddle();
static IContainer BottomValue(IContainer c) => c.Height(9).BorderTop(0.7f).BorderBottom(0.7f).BorderColor(CstColors.LineDark).PaddingHorizontal(2).AlignMiddle();

static class TextExtensions
{
    public static TextBlockDescriptor SemiBold(this TextBlockDescriptor descriptor, bool enabled) => enabled ? descriptor.SemiBold() : descriptor;
}

static class CstColors
{
    public const string Text = "#242424";
    public const string Subtitle = "#444444";
    public const string Line = "#9A9A9A";
    public const string LineDark = "#666666";
    public const string LineLight = "#D9D9D9";
    public const string GroupTotalFill = "#E3E3E3";
    public const string TotalFill = "#F2F2F2";
    public const string Red = "#B11B1B";
    public const string SectionTone = "#F5F5F5";
}

record CstRow(string Group, string Label, string[] Official, string[] Estimate, string Difference, string DifferenceNote, string UpdateFrequency, bool IsSectionDivider = false, bool IsTotal = false, string? GroupTone = null);
record FooterRow(string Label, string[] Values, string Difference);
record CstReportModel(List<CstRow> Rows, List<FooterRow> FooterRows);

static class CstReportData
{
    private static string[] O(params string[] v) => v;

    public static CstReportModel Create()
    {
        var rows = new List<CstRow>
        {
            new("Credit & Country risks", "Mortgages", O("", "", "", "308", "308"), O("", "", "", "312", "312"), "4", "", "Weekly (02/04/25)", true),
            new("Credit & Country risks", "Lombard", O("99", "", "", "317", "416"), O("102", "", "", "315", "417"), "1", "", "Weekly (02/04/25)"),
            new("Credit & Country risks", "Counterparty credit risk", O("421", "186", "12", "1", "621"), O("430", "190", "15", "2", "637"), "16", "", "GRR"),
            new("Credit & Country risks", "Corporates", O("768", "46", "19", "722", "1,555"), O("780", "50", "21", "730", "1,581"), "26", "", "GRR"),
            new("Credit & Country risks", "Loan Underwriting", O("636", "", "", "636", "636"), O("639", "3", "5", "2", "649"), "13", "", "Weekly (03/04/25)"),
            new("Credit & Country risks", "Issuer risk", O("503", "111", "(5)", "0", "609"), O("644", "177", "(5)", "0", "816"), "207", "", "Weekly (31/03/25)"),
            new("Credit & Country risks", "IFRSP ECL", O("(9)", "0", "0", "881", "872"), O("(8)", "1", "0", "885", "878"), "6", "", "GRR"),
            new("Credit & Country risks", "CDF", O("156", "5", "98", "640", "900"), O("160", "6", "100", "645", "911"), "11", "", "GRR"),
            new("Credit & Country risks", "Other credit risk", O("6", "", "47", "460", "513"), O("7", "", "50", "465", "522"), "9", "", "GRR"),
            new("Credit & Country risks", "   o/w ARS / APS", O("", "", "47", "", "47"), O("", "", "48", "", "48"), "1", "", "GRR"),
            new("Market & Investment risks", "Traded Market risk", O("(296)", "(322)", "102", "30", "(485)"), O("(91)", "(270)", "94", "20", "(246)"), "239", "", "Weekly (05/04/25)", true),
            new("Market & Investment risks", "NCL Marketrisk", O("15", "5", "2", "8", "30"), O("18", "6", "3", "10", "37"), "7", "", "GRR"),
            new("Market & Investment risks", "Equity investments", O("196", "1", "43", "1,182", "1,422"), O("200", "2", "45", "1,190", "1,437"), "15", "", "GRR"),
            new("Market & Investment risks", "High-quality liquid assets", O("", "101", "", "", "101"), O("", "90", "", "", "90"), "(11)", "", "Weekly (03/04/25)"),
            new("Market & Investment risks", "Valuation Adjustments", O("518", "14", "92", "", "624"), O("520", "16", "95", "", "631"), "7", "", "GRR"),
            new("Operational Risk", "", O("615", "", "579", "2,045", "3,240"), O("620", "", "585", "2,060", "3,265"), "25", "", "GRR²", true),
            new("Treasury risk", "Funding & liquidity risk", O("262", "8", "11", "638", "919"), O("265", "9", "12", "645", "931"), "12", "", "GRR", true),
            new("Aggregate risk exposure P&L impact - Trigger utilization", "", O("3,874", "151", "998", "7,226", "12,250"), O("4,224", "258", "990", "7,215", "12,688"), "438", "", "GRR", true, true, CstColors.SectionTone),
            new("Treasury risk", "Structural FX", O("", "(979)", "", "", "(979)"), O("", "(980)", "", "", "(980)"), "(1)", "", "Weekly (03/04/25)", true),
            new("Market & Investment risks", "Market Risk (delta CET1 vs P&L)", O("", "(491)", "", "", "(491)"), O("", "(462)", "", "", "(462)"), "29", "", "GRR"),
            new("Market & Investment risks", "PVA (delta CET1 vs P&L)", O("", "252", "", "", "252"), O("", "250", "", "", "250"), "(2)", "", "GRR"),
            new("Aggregate risk exposure (excl. pension risk)", "", O("3,874", "(1,067)", "998", "7,226", "11,031"), O("4,224", "(931)", "990", "7,215", "11,499"), "468", "", "", true, true, CstColors.SectionTone),
        };

        var footerRows = new List<FooterRow>
        {
            new("Current Trigger released", O("4,800", "400", "1,300", "7,900", "14,400", "4,500", "400", "1,300", "7,800", "14,000", "", "", ""), ""),
        };

        return new CstReportModel(rows, footerRows);
    }
}
