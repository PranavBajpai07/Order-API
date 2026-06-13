using System.Text;
using Logistics.OrderApi.Domain;

namespace Logistics.OrderApi.Infrastructure.Csv;

internal static class CsvDocumentReader
{
    public static CsvTable Read(string filePath, IList<DataLoadIssue> issues)
    {
        var sourceFile = Path.GetFileName(filePath);
        var text = File.ReadAllText(filePath, Encoding.UTF8);
        var rawRows = ParseRows(text, sourceFile, issues)
            .Where(row => row.Fields.Any(field => !string.IsNullOrWhiteSpace(field)))
            .ToList();

        if (rawRows.Count == 0)
        {
            issues.Add(new DataLoadIssue(
                "Warning",
                sourceFile,
                null,
                "CSV file is empty."));

            return new CsvTable(sourceFile, [], []);
        }

        var headers = MakeUniqueHeaders(rawRows[0].Fields);
        if (headers.Count == 0)
        {
            issues.Add(new DataLoadIssue(
                "Warning",
                sourceFile,
                rawRows[0].RowNumber,
                "CSV file does not contain a usable header row."));

            return new CsvTable(sourceFile, [], []);
        }

        var rows = new List<CsvDataRow>();
        foreach (var rawRow in rawRows.Skip(1))
        {
            if (rawRow.Fields.Count != headers.Count)
            {
                issues.Add(new DataLoadIssue(
                    "Warning",
                    sourceFile,
                    rawRow.RowNumber,
                    $"Expected {headers.Count} columns but found {rawRow.Fields.Count}. Missing values are set to null; extra values are kept as extraColumn fields."));
            }

            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                values[headers[index]] = CleanValue(index < rawRow.Fields.Count
                    ? rawRow.Fields[index]
                    : null);
            }

            for (var index = headers.Count; index < rawRow.Fields.Count; index++)
            {
                values[$"extraColumn{index - headers.Count + 1}"] = CleanValue(rawRow.Fields[index]);
            }

            rows.Add(new CsvDataRow(rawRow.RowNumber, values));
        }

        return new CsvTable(sourceFile, headers, rows);
    }

    private static List<RawCsvRow> ParseRows(
        string text,
        string sourceFile,
        IList<DataLoadIssue> issues)
    {
        var rows = new List<RawCsvRow>();
        var fields = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;
        var rowStartLine = 1;
        var currentLine = 1;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];

            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        currentField.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (current == '\n')
                    {
                        currentLine++;
                    }

                    currentField.Append(current);
                }

                continue;
            }

            switch (current)
            {
                case '"':
                    if (currentField.Length == 0)
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        currentField.Append(current);
                    }

                    break;
                case ',':
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                    break;
                case '\r':
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    AddRow();
                    currentLine++;
                    rowStartLine = currentLine;
                    break;
                case '\n':
                    AddRow();
                    currentLine++;
                    rowStartLine = currentLine;
                    break;
                default:
                    currentField.Append(current);
                    break;
            }
        }

        if (inQuotes)
        {
            issues.Add(new DataLoadIssue(
                "Warning",
                sourceFile,
                rowStartLine,
                "Quoted field was not closed before the end of the file."));
        }

        if (fields.Count > 0 || currentField.Length > 0)
        {
            fields.Add(currentField.ToString());
            rows.Add(new RawCsvRow(rowStartLine, fields.ToArray()));
        }

        return rows;

        void AddRow()
        {
            fields.Add(currentField.ToString());
            rows.Add(new RawCsvRow(rowStartLine, fields.ToArray()));
            fields.Clear();
            currentField.Clear();
        }
    }

    private static IReadOnlyList<string> MakeUniqueHeaders(IReadOnlyList<string> headers)
    {
        var uniqueHeaders = new List<string>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index].Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                header = $"column{index + 1}";
            }

            if (!seen.TryAdd(header, 1))
            {
                seen[header]++;
                header = $"{header}_{seen[header]}";
            }

            uniqueHeaders.Add(header);
        }

        return uniqueHeaders;
    }

    private static string? CleanValue(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var cleaned = value.Trim();
        return string.IsNullOrEmpty(cleaned) ? null : cleaned;
    }

    private sealed record RawCsvRow(int RowNumber, IReadOnlyList<string> Fields);
}

internal sealed record CsvTable(
    string SourceFile,
    IReadOnlyList<string> Headers,
    IReadOnlyList<CsvDataRow> Rows);

internal sealed record CsvDataRow(
    int RowNumber,
    IReadOnlyDictionary<string, string?> Fields);
