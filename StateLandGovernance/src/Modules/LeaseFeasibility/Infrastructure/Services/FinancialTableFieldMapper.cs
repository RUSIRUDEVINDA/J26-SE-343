using System.Globalization;
using System.Text.RegularExpressions;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

/// <summary>Conservative table-derived suggestions. Every populated field still requires confirmation.</summary>
public static class FinancialTableFieldMapper
{
    public static UploadedFinancialDocumentResult Enrich(UploadedFinancialDocumentResult result, IReadOnlyList<string[][]> tables)
    {
        var fields = result.Fields.ToDictionary(pair => pair.Key, pair => pair.Value);
        var sources = fields.Keys.ToDictionary(key => key, _ => "Explicit summary label in extracted document text; verify against the original.");
        var reasons = result.ReviewReasons.ToList();
        void Suggest(string field, decimal amount, string source)
        {
            if (!fields.ContainsKey(field)) { fields[field] = amount.ToString("0.##", CultureInfo.InvariantCulture); sources[field] = source; }
        }
        if (result.Kind == "bank") MapBank(tables, Suggest, reasons);
        if (result.Kind == "crib") MapCrib(tables, fields, sources, Suggest, reasons);
        if (fields.ContainsKey("income")) reasons.RemoveAll(reason => reason.StartsWith("Monthly income was not explicitly identified."));
        if (fields.ContainsKey("grade")) reasons.RemoveAll(reason => reason.StartsWith("CRIB fields could not be reliably mapped."));
        return result with { Fields = fields, FieldSources = sources, ReviewReasons = reasons.Distinct().ToArray() };
    }

    private sealed record Transaction(DateTime Date, string Description, decimal Credit, decimal Balance);

    private static void MapBank(IReadOnlyList<string[][]> tables, Action<string, decimal, string> suggest, List<string> reasons)
    {
        var transactions = new List<Transaction>();
        var malformed = false;
        foreach (var table in tables)
        {
            if (table.Length < 2) continue;
            var header = table[0].Select(Clean).ToArray();
            var date = Array.FindIndex(header, value => value == "DATE");
            var description = Array.FindIndex(header, value => value is "PARTICULARS" or "DESCRIPTION" or "DETAILS");
            var credit = Array.FindIndex(header, value => value is "CREDITS" or "CREDIT" or "DEPOSITS");
            var balance = Array.FindIndex(header, value => value is "BALANCE" or "RUNNING BALANCE");
            if (new[] { date, description, credit, balance }.Any(index => index < 0)) continue;
            foreach (var row in table.Skip(1))
            {
                if (row.Length <= new[] { date, description, credit, balance }.Max()) { malformed = true; continue; }
                if (string.IsNullOrWhiteSpace(row[date])) continue; // Totals and empty rows.
                if (!DateTime.TryParseExact(row[date].Trim(), new[] { "ddMMMyyyy", "dd-MMM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "dd MMM yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ||
                    !Amount(row[balance], out var accountBalance) || (!string.IsNullOrWhiteSpace(row[credit]) && !Amount(row[credit], out _)))
                { malformed = true; continue; }
                var credited = string.IsNullOrWhiteSpace(row[credit]) ? 0 : Amount(row[credit], out var value) ? value : 0;
                transactions.Add(new(day, Clean(row[description]), credited, accountBalance));
            }
        }
        var months = transactions.GroupBy(transaction => new DateTime(transaction.Date.Year, transaction.Date.Month, 1)).OrderBy(group => group.Key).ToArray();
        if (malformed || months.Length != 6 || months.Where((month, index) => month.Key != months[0].Key.AddMonths(index)).Any() ||
            months.Any(month => month.Count(row => row.Description is "B/F" or "BALANCE BROUGHT FORWARD") != 1 ||
                month.Count(row => row.Description is "C/F" or "BALANCE CARRIED FORWARD") != 1 ||
                !month.Any(row => row.Date.Day == 1 && row.Description is "B/F" or "BALANCE BROUGHT FORWARD") ||
                !month.Any(row => row.Date.Day == DateTime.DaysInMonth(row.Date.Year, row.Date.Month) && row.Description is "C/F" or "BALANCE CARRIED FORWARD")))
        {
            reasons.Add("Bank table calculations require six consecutive complete calendar months, clear dates and balances, and one opening/closing record per month. Incomplete or ambiguous evidence needs manual review.");
            return;
        }
        var salary = months.Select(month => month.Where(row => row.Credit > 0 &&
            Regex.IsMatch(row.Description, @"\b(SAL|SALARY|WAGES|PAYROLL|PENSION)\b") &&
            !Regex.IsMatch(row.Description, @"\b(TRANSFER|REFUND|LOAN|REVERSAL)\b")).Sum(row => row.Credit)).ToArray();
        if (salary.Any(amount => amount > 0))
            suggest("income", Math.Round(salary.Average(), 2), "Sum of identifiable salary/wage/pension credits divided by six complete statement months. Transfers, refunds and loan credits excluded; verify other income sources separately.");
        else reasons.Add("No confidently identifiable salary/wage/pension credits. Classify and verify other income sources manually.");
        var dailyBalances = new List<decimal>();
        foreach (var month in months)
        {
            var endOfDay = month.GroupBy(row => row.Date.Date).ToDictionary(group => group.Key, group => group.Last().Balance);
            var current = month.Single(row => row.Description is "B/F" or "BALANCE BROUGHT FORWARD").Balance;
            for (var day = month.Key; day.Month == month.Key.Month; day = day.AddDays(1))
            {
                if (endOfDay.TryGetValue(day, out var amount)) current = amount;
                dailyBalances.Add(current);
            }
        }
        suggest("balance", Math.Round(dailyBalances.Average(), 2), "Average daily closing balance across the six complete statement months; the last balance on each date is carried through days without transactions.");
        var episodes = 0;
        for (var index = 0; index < dailyBalances.Count; index++)
            if (dailyBalances[index] < 0 && (index == 0 || dailyBalances[index - 1] >= 0)) episodes++;
        suggest("overdrafts", episodes, "Suggested count of separate negative daily closing-balance episodes during the six-month period. Verify against the agreed overdraft evidence rule.");
        reasons.Add("Income consistency requires a reviewed upstream calculation; no ratio is invented from the transaction table. Requested lease payment is supplied separately by the applicant.");
    }

    private static void MapCrib(IReadOnlyList<string[][]> tables, Dictionary<string, string> fields,
        Dictionary<string, string> sources, Action<string, decimal, string> suggest, List<string> reasons)
    {
        var grades = new HashSet<string>();
        var debt = 0m;
        var foundFacilities = false;
        var completeDebt = true;
        var facilityIds = new HashSet<string>();
        foreach (var table in tables)
        {
            if (table.Length < 2) continue;
            var header = table[0].Select(Clean).ToArray();
            var gradeColumn = Array.IndexOf(header, "RISK GRADE");
            if (gradeColumn >= 0 && header.Contains("SCORE") && header.Contains("PROBABILITY OF DEFAULT"))
                foreach (var row in table.Skip(1))
                    if (row.Length > gradeColumn && Regex.IsMatch(Clean(row[gradeColumn]), @"^(XX|[A-E][1-3]?)$")) grades.Add(Clean(row[gradeColumn]));
            var rows = table.Where(row => row.Length > 1).GroupBy(row => Clean(row[0])).ToDictionary(group => group.Key, group => group.First());
            if (!rows.TryGetValue("CREDIT FACILITY INDEX", out var facilities)) continue;
            foundFacilities = true;
            string Cell(string label, int column) => rows.TryGetValue(label, out var row) && column < row.Length ? Clean(row[column]) : "";
            for (var column = 1; column < facilities.Length; column++)
            {
                var status = Cell("CREDIT FACILITY STATUS", column);
                if (status is "CLOSED" or "CLSD" or "SETTLED") continue;
                var ownership = Cell("OWNERSHIP INDICATOR", column);
                if (ownership == "GRT") continue; // Guarantor exposure requires a separate review.
                var id = Cell("MEMBER", column) + ":" + Clean(facilities[column]);
                if (!facilityIds.Add(id)) { completeDebt = false; continue; }
                if (status != "ACTV" || ownership is not ("OWN" or "JNT") || Cell("CURRENCY", column) != "LKR" ||
                    Cell("REPAYMENT TYPE", column) != "MNLY" || !Amount(Cell("INSTALLMENT AMOUNT", column), out var installment) || installment < 0)
                { completeDebt = false; continue; }
                debt += installment;
            }
        }
        if (grades.Count == 1)
        {
            var grade = grades.Single();
            if (!fields.ContainsKey("grade") || fields["grade"] == grade)
            { fields["grade"] = grade; sources["grade"] = "Current CRIB score table's Risk Grade column; original bureau subgrade retained."; }
            else { fields.Remove("grade"); sources.Remove("grade"); reasons.Add("Text and table CRIB grades conflict; verify the original report."); }
        }
        else if (grades.Count > 1) { fields.Remove("grade"); sources.Remove("grade"); reasons.Add("Multiple current CRIB grades were found; verify the original report."); }
        if (foundFacilities && completeDebt)
            suggest("debt", debt, "Sum of explicit LKR monthly installments for active borrower/joint-borrower facilities. Principal balances and guaranteed facilities are excluded; verify all obligations.");
        else if (foundFacilities) reasons.Add("Monthly debt remains unknown: at least one CRIB facility has an unclear status, ownership, currency, repayment frequency or installment. Correct against the original; no partial total was substituted.");
        if (!fields.ContainsKey("defaultHistory")) reasons.Add("Default history needs review of facility statuses and repayment history; a good grade or zero current arrears does not prove no past default.");
    }

    private static string Clean(string value) => Regex.Replace(value.Trim().ToUpperInvariant(), @"\s+", " ");
    private static bool Amount(string text, out decimal amount)
    {
        amount = 0;
        var match = Regex.Match(text.Trim(), @"^(?:LKR\s*)?(-?[0-9]+(?:,[0-9]{3})*(?:\.[0-9]{1,2})?)\s*(Cr|Dr)?$", RegexOptions.IgnoreCase);
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount)) return false;
        if (match.Groups[2].Value.Equals("Dr", StringComparison.OrdinalIgnoreCase)) amount = -Math.Abs(amount);
        return true;
    }
}
