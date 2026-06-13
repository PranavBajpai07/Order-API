namespace Logistics.OrderApi.Infrastructure.Csv;

public sealed class OrderDataOptions
{
    public const string SectionName = "OrderData";

    public string Path { get; init; } = "Data";

    public bool IncludeContentRootCsvFilesWhenDataFolderIsEmpty { get; init; } = true;

    public int MaxPageSize { get; init; } = 200;
}
