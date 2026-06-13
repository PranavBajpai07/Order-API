namespace Logistics.OrderApi.Domain;

public sealed record OrderBrowseRequest(
    int Skip,
    int Take,
    string? Query,
    string? Status);
