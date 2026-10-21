using System.Globalization;
using System.Text.RegularExpressions;
using StateLandGovernance.LeaseFeasibility.Application.Proposals;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Proposals;

public sealed record ProposalFormBlock(string Kind, string Text, string[]? Headers = null,
    string[][]? Rows = null, double[]? Widths = null, int Height = 0, bool Border = true);
public sealed record ProposalFormPage(int Number, IReadOnlyList<ProposalFormBlock> Blocks);

// One layout definition drives Word and the printable HTML. Pages follow the supplied 11-page form.
public static class LandDepartmentProposalForm
{
    public const string LayoutVersion = "land-department-form-11-pages-v1";
    public static IReadOnlyList<ProposalFormPage> Build(string text, bool blankForm = false)
    {
        var fields = Parse(text, blankForm);
        string V(string key) => fields.GetValueOrDefault(key, "");
        string D(string key) => string.IsNullOrWhiteSpace(V(key)) ? "........................................................................" : V(key);
        string Money(string key) => V(key).Replace("LKR ", "");
        if (V("purpose") != "" && V("purpose") != V("projectType")) fields["projectType"] = string.Join("\n", new[] { V("projectType"), V("purpose") }.Where(value => value != ""));
        var pages = new List<ProposalFormPage>();
        var blocks = new List<ProposalFormBlock>();
        void Page() { if (blocks.Count > 0) pages.Add(new(pages.Count + 1, blocks.ToArray())); blocks.Clear(); }
        void H(string value) => blocks.Add(new("heading", value));
        void P(string value) => blocks.Add(new("paragraph", value));
        void F(string number, string label, string key) => blocks.Add(new("field", $"{number} {label}", Rows: new[] { new[] { D(key) } }));
        void Lines(string label, string key, int lines) => blocks.Add(new("lines", label, Rows: new[] { new[] { V(key) } }, Height: lines));
        void Box(string label, string key, int height) => blocks.Add(new("box", label, Rows: new[] { new[] { V(key) } }, Height: height));
        void T(string[] headers, IEnumerable<string[]> rows, double[] widths, int height = 19, bool border = true) => blocks.Add(new("table", "", headers, rows.ToArray(), widths, height, border));
        void Grid(string key, string[] headers, double[] widths, int count, int height = 22, string[]? numberedDefaults = null)
        {
            var raw = V(key).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var parsed = raw.Select(line => line.Split('|', StringSplitOptions.TrimEntries)).ToArray();
            var tabular = parsed.Length > 0 && parsed.All(row => row.Length == headers.Length);
            var rows = tabular ? parsed.ToList() : new List<string[]>();
            while (rows.Count < count)
            {
                var row = new string[headers.Length]; Array.Fill(row, "");
                if (headers[0].StartsWith("අනු")) row[0] = (rows.Count + 1).ToString("00");
                if (key == "schedule" && rows.Count < 2) row[0] = rows.Count == 0 ? "ඉඩම සැකසීම" : "ජලය සැපයීම";
                if (numberedDefaults != null && rows.Count < numberedDefaults.Length) row[1] = numberedDefaults[rows.Count];
                rows.Add(row);
            }
            T(headers, rows, widths, height);
            if (!tabular && raw.Length > 0) P("සටහන්: " + V(key));
        }
        void Areas(string permanent, string temporary)
        {
            Lines("• ස්ථිර ඉදිකිරීම්", permanent, 3);
            Lines("• තාවකාලික ඉදිකිරීම්", temporary, 3);
            P("අදාළ ඉදිකිරීම්වල වර්ග ප්‍රමාණය (වර්ග මීටර්වලින්)");
            T(new[] { "ගොඩනැගිල්ල", "වර්ග ප්‍රමාණය (මී²)" }, new[] {
                new[] { "A ගොඩනැගිල්ල", "" }, new[] { "B ගොඩනැගිල්ල", "" }, new[] { "C ගොඩනැගිල්ල", "" }, new[] { "D ගොඩනැගිල්ල", "" }, new[] { "එකතුව", "" }
            }, new double[] { 60, 40 }, 21, false);
        }
        H("කෘෂිකාර්මික හා වාණිජ කටයුතු වෙනුවෙන් ඉඩම් දිගු කාලීන බදු ඉල්ලුම් කිරීමේදී ඉදිරිපත් කළයුතු ව්‍යාපෘති වාර්තාව");
        H("01. පුද්ගලික තොරතුරු");
        F("1.1", "නම", "applicantName"); F("1.2", "ලිපිනය", "address");
        F("1.3", "පදිංචි ග්‍රාම නිලධාරී වසම", "gnDivision"); F("1.4", "ප්‍රාදේශීය ලේකම් කොට්ඨාසය", "dsDivision");
        P($"1.5 දුරකථන අංකය: {D("mobile")} (ජංගම)   {D("landline")} (ස්ථාවර)");
        F("1.6", "හැඳුනුම්පත් අංකය", "nic"); P($"1.7 උපන්දිනය: {D("birthDate")}    වයස: {D("age")}");
        F("1.8", "ස්ත්‍රී / පුරුෂ භාවය", "gender"); F("1.9", "විවාහක / අවිවාහක තත්ත්වය", "maritalStatus");
        F("1.10", "අධ්‍යාපන සුදුසුකම්", "education"); P("1.11 වෘත්තීය සුදුසුකම්");
        F("•", "හදාරා ඇති වෘත්තීය පාඨමාලා", "vocational"); F("•", "තාක්ෂණික දැනුම", "technicalKnowledge");
        Lines("• ප්‍රායෝගික දැනුම හා පළපුරුද්ද", "experience", 2);
        H("02. ව්‍යාපෘතියේ මූලික තොරතුරු"); F("2.1", "ව්‍යාපෘතියේ නම", "projectName");
        F("2.2", "ව්‍යාපෘතියේ ස්වභාවය", "projectType"); Lines("2.3 ව්‍යාපෘතිය පිළිබඳ කෙටි හැඳින්වීමක්", "overview", 4);
        Page();
        H("02. ව්‍යාපෘතියේ මූලික තොරතුරු (ඉතිරිය)");
        P("2.4 ව්‍යාපෘතිය ආරම්භ කිරීමට අපේක්ෂිත ඉඩම පිහිටි ස්ථානය");
        F("•", "ප්‍රාදේශීය ලේකම් කොට්ඨාසය", "landDs"); F("•", "ග්‍රාම නිලධාරී වසම", "landGn");
        F("•", "පළාත් පාලන බල ප්‍රදේශය", "localAuthority"); F("•", "ඉඩමේ ප්‍රමාණය", "landExtent");
        Lines("2.5 දැනටමත් ආරම්භ කර ඇති ව්‍යාපාරයක් නම් ලියාපදිංචි නාමය, අංකය හා දිනය", "registration", 2);
        F("2.6", "ව්‍යාපාරයේ අයිතියේ ස්වභාවය (තනි පුද්ගල / හවුල් / සමාගම)", "ownership");
        H("2.7 ඉඩමේ සිදු කරනු ලබන ඉදිකිරීම් පිළිබඳ තොරතුරු");
        P("i. බදු පදනම මත ඉඩම ලබා ගැනීමෙන් අනතුරුව මූලික අදියර යටතේ ඉදිකිරීමට අපේක්ෂිත ගොඩනැගිලි හා වෙනත් ඉදිකිරීම්");
        Areas("initialPermanent", "initialTemporary"); Page();
        H("2.8 මූලික අදියර යටතේ ඉදිකිරීමට අපේක්ෂිත ගොඩනැගිලි හා ඉදිකිරීම් දැක්වෙන දළ සැලැස්ම");
        Box("ඉඩමේ මායිම්, විශේෂ ස්ථාන හා ගොඩනැගිලි ඇතුළත් කරන්න", "initialLayout", 240);
        P("ii. බදු පදනම මත ඉඩම ලබා ගැනීමෙන් අනතුරුව ඉදිරි වර්ෂ 15 තුළ ඉදිකිරීමට අපේක්ෂිත ගොඩනැගිලි හා වෙනත් ඉදිකිරීම්");
        Areas("futurePermanent", "futureTemporary"); Page();
        H("2.9 ඉදිරි වර්ෂ 15 තුළ අපේක්ෂිත සංවර්ධනය සහිත ගොඩනැගිලි හා ඉදිකිරීම් දැක්වෙන දළ සැලැස්ම");
        Box("ඉඩමේ මායිම්, විශේෂ ස්ථාන හා ගොඩනැගිලි ඇතුළත් කරන්න", "futureLayout", 260);
        H("2.10 ව්‍යාපෘතියේ ඉඩම හා මාර්ගය පිහිටි ස්ථානය (ආසන්න නගරයේ සිට) දළ සටහනකින් දක්වන්න");
        Box("ප්‍රවේශ මාර්ගය හා ආසන්න නගරය", "accessMap", 235); Page();
        H("2.11 ලබා දීමට අපේක්ෂිත ඍජු හා වක්‍ර රැකියා සංඛ්‍යාව");
        Grid("employment", new[] { "රැකියාවේ ස්වභාවය / තනතුර", "ස්ත්‍රී", "පුරුෂ" }, new double[] { 64,18,18 }, 5, 20);
        H("03. නිෂ්පාදනය සඳහා අවශ්‍ය අමුද්‍රව්‍ය හා ඒවා සපයා ගන්නා ආකාරය (අදාළ වන්නේ නම් පමණි)");
        Grid("materials", new[] { "අනු අංකය", "අවශ්‍ය ප්‍රධාන අමුද්‍රව්‍ය වර්ගය", "සපයා ගන්නා ආකාරය" }, new double[] { 10,45,45 }, 7, 20);
        H("04. අමුද්‍රව්‍ය වියදම් ඇස්තමේන්තුව");
        Grid("materialCosts", new[] { "විස්තරය", "ඒකකය", "ඒකකයක වියදම (රු)", "මසකට අවශ්‍යතාවය (ඒකක)", "මාසික වියදම (රු)", "වාර්ෂික වියදම (රු)" }, new double[] { 26,10,16,16,16,16 }, 9, 19);
        P("ඉඩකඩ ප්‍රමාණවත් නොවන විට ඇමුණුමක් ලෙස ඉදිරිපත් කරන්න."); Page();
        H("05. අලෙවි තොරතුරු"); Lines("5.1 වර්තමාන වෙළඳපොළ තත්ත්වය", "currentMarket", 6);
        P("5.2 අනාගත වෙළඳපොළ ප්‍රවර්ධන සැලැස්ම");
        Lines("• දේශීය", "domesticMarket", 6); Lines("• විදේශීය", "exportMarket", 6);
        H("06. ව්‍යාපෘතියේ මූලික පිළිවෙළ"); Lines("අපේක්ෂිත මාසික / වාර්ෂික ආදායම් හා අස්වැන්න පිළිබඳ විස්තර", "operations", 9); Page();
        H("07. අලෙවි ආදායම් ඇස්තමේන්තුව");
        Grid("salesPlan", new[] { "විස්තරය", "ඒකකය", "ඒකකයක මිල (රු)", "මසකට අලෙවි ප්‍රමාණය (ඒකක)", "මාසික ආදායම (රු)", "වාර්ෂික ආදායම (රු)" }, new double[] { 26,10,16,16,16,16 }, 10, 21);
        if (V("Applicant-estimated monthly sales") != "") P($"මුළු මාසික ආදායම: {Money("Applicant-estimated monthly sales")}   වාර්ෂික: {Money("Annual sales assuming 12 identical operating months")}");
        H("08. කාරක ප්‍රාග්ධන අවශ්‍යතාවය (මසකට)"); P("අමුද්‍රව්‍ය / කම්කරු කුලී / ඇසුරුම් / ඉන්ධන / පොහොර ආදිය");
        Grid("workingCapital", new[] { "අනු අංකය", "විස්තරය", "මුදල (රු)" }, new double[] { 10,65,25 }, 10, 21);
        T(new[] { "", "", "" }, new[] { new[] { "", "මුළු වියදම", Money("Monthly working capital subtotal (production + operating costs)") } }, new double[] { 10,65,25 }, 21);
        Page();
        H("09. ව්‍යාපෘතියේ මුළු පිරිවැය සහ එය සපයා ගන්නා ආකාරය (මුළු ආයෝජනය)");
        Grid("investmentPlan", new[] { "අනු අංකය", "විස්තරය", "මුළු වියදම (රු)", "දැනටමත් දරා ඇති මුදල (රු)", "තමන් යෙදවීමට අපේක්ෂිත මුදල (රු)", "හිඟ මුදල හා සපයාගන්නා ආකාරය" },
            new double[] { 8,27,16,16,16,17 }, 15, 23,
            new[] { "ඉඩම හා ඉඩම සංවර්ධනය (යටිතල පහසුකම්)", "ගොඩනැගිලි", "යන්ත්‍ර සූත්‍ර", "ලී බඩු හා උපකරණ", "වෙනත්" });
        T(new[] { "", "", "", "", "", "" }, new[] { new[] { "", "එකතුව", Money("Initial investment"), "", Money("Planned own funding"), Money("Remaining funding requirement") } }, new double[] { 8,27,16,16,16,17 }, 23);
        if (V("fundingPlan") != "") P("මුදල් සපයාගන්නා ආකාරය: " + V("fundingPlan"));
        H("10. අපේක්ෂිත මුළු නිෂ්පාදන ප්‍රමාණය (මාසික / වාර්ෂික)");
        Lines("කිරි ලීටර් / පොල් ගෙඩි සංඛ්‍යාව / සේවා ප්‍රමාණය ආදී වශයෙන්", "production", 6); Page();
        H("11. ලාභ අලාභ ඇස්තමේන්තුව");
        var profitRows = new List<string[]> { new[] { "මුළු ආදායම", Money("Applicant-estimated monthly sales"), Money("Annual sales assuming 12 identical operating months") }, new[] { "අඩු කළා (නිෂ්පාදන වියදම්)", "", "" } };
        string[] productionLabels = { "අමුද්‍රව්‍ය වියදම", "අමුද්‍රව්‍ය ගෙන ඒමේ වියදම", "ඉන්ධන / බලය", "කම්කරු කුලිය / සෘජු ශ්‍රමය", "ඇසුරුම් / දැවටුම්" };
        for (var i = 0; i < 12; i++) profitRows.Add(new[] { $"{i + 1}. {(i < productionLabels.Length ? productionLabels[i] : "")}", "", "" });
        profitRows.Add(new[] { "මුළු නිෂ්පාදන වියදම", Money("Applicant-estimated monthly production costs"), Money("Annual production costs (12 equal months)") });
        profitRows.Add(new[] { "දළ ලාභය", Money("Monthly gross profit"), Annual(Money("Monthly gross profit")) });
        profitRows.Add(new[] { "අඩු කළා (පවත්වාගෙන යාමේ / වක්‍ර / මෙහෙයුම් වියදම්)", "", "" });
        string[] operatingLabels = { "වේතන", "ප්‍රවාහන වියදම්", "කුලිය", "අලෙවි වියදම්", "විදුලි බිල්", "දුරකථන බිල්", "ණය පොලී", "ක්ෂය වීම" };
        for (var i = 0; i < 12; i++) profitRows.Add(new[] { $"{i + 1}. {(i < operatingLabels.Length ? operatingLabels[i] : "")}", "", "" });
        profitRows.Add(new[] { "මුළු මෙහෙයුම් වියදම", Money("Applicant-estimated monthly operating costs"), Money("Annual operating costs (12 equal months)") });
        profitRows.Add(new[] { "ශුද්ධ ලාභය", Money("Monthly net profit"), Money("Annual net profit (12 equal months)") });
        T(new[] { "විස්තරය", "මාසික (රු)", "වාර්ෂික (රු)" }, profitRows, new double[] { 52,24,24 }, 16);
        if (V("Annual sales assuming 12 identical operating months") != "") P("වාර්ෂික ඇස්තමේන්තු සමාන මෙහෙයුම් මාස 12කට අදාළය.");
        if (V("profitAssumptions") != "") P(V("profitAssumptions")); Page();
        H("12. ක්‍රියාත්මක සැලැස්ම"); P("පළමු වර්ෂය / දෙවන වර්ෂය");
        Grid("schedule", new[] { "ක්‍රියාකාරකම", "ජන", "පෙබ", "මාර්", "අප්‍රේ", "මැයි", "ජූනි", "ජූලි", "අගෝ", "සැප්", "ඔක්", "නොවැ", "දෙසැ", "වගකීම" },
            new double[] { 23,5,5,5,5,5,5,5,5,5,5,5,5,17 }, 22, 19);
        Lines("වෙනත් සටහන්", "scheduleNotes", 5); Page();
        H("13. පාරිසරික බලපෑම"); Lines("", "environment", 11);
        H("14. ව්‍යාපෘතියේ තිරසර භාවය හා නඩත්තුව"); Lines("", "sustainability", 10);
        P("හිමිකරුගේ අත්සන: ........................................................................");
        F("", "දිනය", "signatureDate"); F("", "දුරකථන අංකය", "signaturePhone"); Page();
        // Keep generated narratives, free-form table notes and unrecognized edits in an annexure rather than dropping them.
        var extra = V("annexure");
        if (extra != "") { H("ඇමුණුම - අමතර ව්‍යාපෘති විස්තර"); P(extra); Page(); }
        return pages;
    }
    private static string Annual(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount is >= -1_000_000_000_000m and <= 1_000_000_000_000m
        ? (amount * 12).ToString("N2", CultureInfo.InvariantCulture) : "";

    public static Dictionary<string, string> Parse(string text, bool blankForm = false)
    {
        if (blankForm) return new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text) || text.Length > 250000) throw new ArgumentException("Provide the saved proposal draft, up to 250000 characters.");
        var headings = Regex.Matches(text, @"(?m)^([0-9]{2})\. ");
        if (!headings.Select(m => int.Parse(m.Groups[1].Value)).SequenceEqual(Enumerable.Range(1, 14)))
            throw new ArgumentException("The form export requires the 14 numbered section headings. Restore the headings or regenerate the draft.");
        var labelKeys = LandDepartmentProposalTemplate.Sections.SelectMany(s => s.Fields).ToDictionary(f => f.Label, f => f.Key);
        labelKeys["Project"] = "projectName"; labelKeys["Purpose"] = "purpose";
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var current = "";
        var annexure = new List<string>();
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            if (line == "REVIEW NOTES") break;
            if (Regex.IsMatch(line, @"^[0-9]{2}\. ") || line.StartsWith("Required information still missing:") || line.StartsWith("Source [")) { current = ""; continue; }
            if (line.StartsWith("AI narrative suggestion") || line.StartsWith("User-confirmed financial assessment")) { current = "annexure"; continue; }
            var colon = line.IndexOf(':');
            if (colon >= 0 && (labelKeys.ContainsKey(line[..colon]) || line.StartsWith("Applicant-estimated ") || line.StartsWith("Monthly ") || line.StartsWith("Annual ") || line.StartsWith("Initial investment:") || line.StartsWith("Planned own funding:") || line.StartsWith("Remaining funding requirement:")))
            {
                var label = line[..colon]; current = labelKeys.GetValueOrDefault(label, label);
                var value = line[(colon + 1)..].Trim();
                result[current] = value == "[TO BE CONFIRMED]" ? "" : value;
            }
            else if (current == "annexure") { if (!string.IsNullOrWhiteSpace(line)) annexure.Add(line); }
            else if (current != "" && !string.IsNullOrWhiteSpace(line)) result[current] += "\n" + line;
            else if (headings.Count > 0 && !string.IsNullOrWhiteSpace(line) && !line.StartsWith("Application:") && !line.StartsWith("Template:") && !line.StartsWith("Generated:") && !line.StartsWith("PROJECT PROPOSAL") && line != text.Split('\n')[0]) annexure.Add(line);
        }
        if (annexure.Count > 0) result["annexure"] = string.Join("\n", annexure);
        return result;
    }
}
