using Logistics.OrderApi.Domain;
using Microsoft.Extensions.Options;

namespace Logistics.OrderApi.Infrastructure.Csv;

public sealed class CsvOrderRepository : IOrderRepository
{
    private readonly IReadOnlyList<Order> _orders;
    private readonly Dictionary<string, Order> _ordersById;
    private readonly int _maxPageSize;

    public CsvOrderRepository(
        IOptions<OrderDataOptions> options,
        IWebHostEnvironment environment,
        ILogger<CsvOrderRepository> logger)
    {
        var settings = options.Value;
        _maxPageSize = Math.Max(1, settings.MaxPageSize);

        var issues = new List<DataLoadIssue>();
        var dataPath = ResolveDataPath(environment.ContentRootPath, settings.Path);
        var csvFiles = FindCsvFiles(dataPath, environment.ContentRootPath, settings);
        var builders = new Dictionary<string, OrderBuilder>(StringComparer.OrdinalIgnoreCase);
        var rowCount = 0;

        if (csvFiles.Count == 0)
        {
            issues.Add(new DataLoadIssue(
                "Warning",
                dataPath,
                null,
                "No CSV files were found. Add files to the configured OrderData:Path folder or set ORDERDATA__PATH."));
        }

        foreach (var csvFile in csvFiles)
        {
            try
            {
                var table = CsvDocumentReader.Read(csvFile, issues);
                foreach (var row in table.Rows)
                {
                    rowCount++;
                    var orderId = CsvOrderMapping.GetOrderId(row.Fields);
                    if (string.IsNullOrWhiteSpace(orderId))
                    {
                        issues.Add(new DataLoadIssue(
                            "Warning",
                            table.SourceFile,
                            row.RowNumber,
                            "Row skipped because no order id column/value could be identified."));
                        continue;
                    }

                    if (!builders.TryGetValue(orderId, out var builder))
                    {
                        builder = new OrderBuilder(orderId);
                        builders[orderId] = builder;
                    }

                    builder.Add(table.SourceFile, row.RowNumber, row.Fields);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to load CSV file {CsvFile}", csvFile);
                issues.Add(new DataLoadIssue(
                    "Error",
                    Path.GetFileName(csvFile),
                    null,
                    $"File could not be loaded: {exception.Message}"));
            }
        }

        _orders = builders.Values
            .Select(builder => builder.Build())
            .OrderBy(order => order.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _ordersById = _orders.ToDictionary(
            order => order.Id,
            StringComparer.OrdinalIgnoreCase);

        Diagnostics = new DataLoadDiagnostics(
            DateTimeOffset.UtcNow,
            dataPath,
            csvFiles.Count,
            rowCount,
            _orders.Count,
            issues);
    }

    public DataLoadDiagnostics Diagnostics { get; }

    public PagedResult<OrderSummary> Browse(OrderBrowseRequest request)
    {
        var take = Math.Min(request.Take, _maxPageSize);
        var orders = _orders.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            orders = orders.Where(order =>
                string.Equals(order.Status, request.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var query = request.Query.Trim();
            orders = orders.Where(order =>
                order.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || order.OrderNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                || order.CustomerId?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                || order.CustomerName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                || CsvOrderMapping.ContainsSearchText(order.Attributes, query));
        }

        var matchingOrders = orders.ToArray();
        var page = matchingOrders
            .Skip(request.Skip)
            .Take(take)
            .Select(ToSummary)
            .ToArray();

        return new PagedResult<OrderSummary>(
            page,
            matchingOrders.Length,
            request.Skip,
            take);
    }

    public Order? GetById(string id)
    {
        return _ordersById.GetValueOrDefault(id);
    }

    private static string ResolveDataPath(string contentRootPath, string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "Data";
        }

        return Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath));
    }

    private static IReadOnlyList<string> FindCsvFiles(
        string dataPath,
        string contentRootPath,
        OrderDataOptions settings)
    {
        if (Directory.Exists(dataPath))
        {
            var files = Directory
                .EnumerateFiles(dataPath, "*.csv", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length > 0 || !settings.IncludeContentRootCsvFilesWhenDataFolderIsEmpty)
            {
                return files;
            }
        }

        if (!settings.IncludeContentRootCsvFilesWhenDataFolderIsEmpty)
        {
            return [];
        }

        return Directory
            .EnumerateFiles(contentRootPath, "*.csv", SearchOption.TopDirectoryOnly)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static OrderSummary ToSummary(Order order)
    {
        return new OrderSummary(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.CustomerName,
            order.Status,
            order.CreatedAt,
            order.TotalAmount,
            order.Currency,
            order.LineCount,
            order.RecordCount,
            order.SourceFiles);
    }

    private sealed class OrderBuilder
    {
        private readonly Dictionary<string, string?> _attributes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<OrderLine> _lines = [];
        private readonly List<OrderRecord> _records = [];
        private readonly SortedSet<string> _sourceFiles = new(StringComparer.OrdinalIgnoreCase);

        private string? _orderNumber;
        private string? _customerId;
        private string? _customerName;
        private string? _status;
        private string? _createdAt;
        private string? _updatedAt;
        private decimal? _totalAmount;
        private string? _currency;

        public OrderBuilder(string id)
        {
            Id = id;
        }

        private string Id { get; }

        public void Add(
            string sourceFile,
            int rowNumber,
            IReadOnlyDictionary<string, string?> fields)
        {
            _sourceFiles.Add(sourceFile);
            _records.Add(new OrderRecord(
                sourceFile,
                rowNumber,
                new Dictionary<string, string?>(fields, StringComparer.OrdinalIgnoreCase)));

            foreach (var (key, value) in fields)
            {
                if (value is not null && !_attributes.ContainsKey(key))
                {
                    _attributes[key] = value;
                }
            }

            var values = CsvOrderMapping.GetSemanticValues(fields);
            _orderNumber ??= values.OrderNumber;
            _customerId ??= values.CustomerId;
            _customerName ??= values.CustomerName;
            _status ??= values.Status;
            _createdAt ??= values.CreatedAt;
            _updatedAt ??= values.UpdatedAt;
            _totalAmount ??= values.TotalAmount;
            _currency ??= values.Currency;

            var lineValues = CsvOrderMapping.GetLineValues(fields);
            if (lineValues.HasLineData)
            {
                _lines.Add(new OrderLine(
                    lineValues.LineNumber,
                    lineValues.ProductNumber,
                    lineValues.Quantity,
                    lineValues.Name,
                    lineValues.Description,
                    lineValues.UnitPrice,
                    lineValues.ProductGroup,
                    lineValues.LineTotal,
                    new Dictionary<string, string?>(fields, StringComparer.OrdinalIgnoreCase),
                    sourceFile,
                    rowNumber));
            }
        }

        public Order Build()
        {
            var computedTotal = _lines.Count > 0 && _lines.All(line => line.LineTotal.HasValue)
                ? _lines.Sum(line => line.LineTotal!.Value)
                : (decimal?)null;

            return new Order(
                Id,
                _orderNumber,
                _customerId,
                _customerName,
                _status,
                _createdAt,
                _updatedAt,
                _totalAmount ?? computedTotal,
                _currency,
                _attributes,
                _lines
                    .OrderBy(line => line.LineNumber, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                _records,
                _sourceFiles.ToArray());
        }
    }
}
