namespace Logistics.OrderApi.Domain;

public sealed record Order(
    string Id,
    string? OrderNumber,
    string? CustomerId,
    string? CustomerName,
    string? Status,
    string? CreatedAt,
    string? UpdatedAt,
    decimal? TotalAmount,
    string? Currency,
    IReadOnlyDictionary<string, string?> Attributes,
    IReadOnlyList<OrderLine> Lines,
    IReadOnlyList<OrderRecord> Records,
    IReadOnlyList<string> SourceFiles)
{
    public int RecordCount => Records.Count;

    public int LineCount => Lines.Count;
}

public sealed record OrderSummary(
    string Id,
    string? OrderNumber,
    string? CustomerId,
    string? CustomerName,
    string? Status,
    string? CreatedAt,
    decimal? TotalAmount,
    string? Currency,
    int LineCount,
    int RecordCount,
    IReadOnlyList<string> SourceFiles);

public sealed record OrderLine(
    string? LineNumber,
    string? ProductNumber,
    decimal? Quantity,
    string? Name,
    string? Description,
    decimal? UnitPrice,
    string? ProductGroup,
    decimal? LineTotal,
    IReadOnlyDictionary<string, string?> Attributes,
    string SourceFile,
    int RowNumber);

public sealed record OrderRecord(
    string SourceFile,
    int RowNumber,
    IReadOnlyDictionary<string, string?> Fields);
