namespace Logistics.OrderApi.Domain;

public interface IOrderRepository
{
    PagedResult<OrderSummary> Browse(OrderBrowseRequest request);

    Order? GetById(string id);

    DataLoadDiagnostics Diagnostics { get; }
}
