namespace Logistics.OrderApi.Domain;

public sealed record DataLoadDiagnostics(
    DateTimeOffset LoadedAt,
    string DataPath,
    int CsvFileCount,
    int RowCount,
    int OrderCount,
    IReadOnlyList<DataLoadIssue> Issues);

public sealed record DataLoadIssue(
    string Severity,
    string SourceFile,
    int? RowNumber,
    string Message);
