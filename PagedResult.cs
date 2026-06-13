namespace Logistics.OrderApi.Domain;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Skip,
    int Take)
{
    public bool HasMore => Skip + Items.Count < Total;
}
