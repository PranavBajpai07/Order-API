using System.Globalization;

namespace Logistics.OrderApi.Infrastructure.Csv;

internal static class CsvOrderMapping
{
    private static readonly string[] OrderIdAliases =
    [
        "orderid",
        "orderno",
        "ordernumber",
        "orderreference",
        "orderref",
        "order",
        "id"
    ];

    private static readonly string[] OrderNumberAliases =
    [
        "orderno",
        "ordernumber",
        "orderreference",
        "orderref"
    ];

    private static readonly string[] CustomerIdAliases =
    [
        "customerid",
        "customerno",
        "customernumber",
        "clientid",
        "buyerid"
    ];

    private static readonly string[] CustomerNameAliases =
    [
        "customername",
        "customer",
        "client",
        "clientname",
        "buyer",
        "buyername",
        "consignee",
        "consigneename"
    ];

    private static readonly string[] StatusAliases =
    [
        "status",
        "orderstatus",
        "state"
    ];

    private static readonly string[] CreatedAtAliases =
    [
        "createdat",
        "createddate",
        "created",
        "orderdate",
        "date",
        "placedat",
        "placeddate"
    ];

    private static readonly string[] UpdatedAtAliases =
    [
        "updatedat",
        "updateddate",
        "modifiedat",
        "modifieddate",
        "lastupdated"
    ];

    private static readonly string[] TotalAmountAliases =
    [
        "totalamount",
        "total",
        "amount",
        "ordervalue",
        "value",
        "grandtotal"
    ];

    private static readonly string[] CurrencyAliases =
    [
        "currency",
        "currencycode",
        "curr"
    ];

    private static readonly string[] LineNumberAliases =
    [
        "orderlinenumber",
        "linenumber",
        "orderline",
        "line"
    ];

    private static readonly string[] ProductNumberAliases =
    [
        "productnumber",
        "productno",
        "sku",
        "itemnumber",
        "itemno"
    ];

    private static readonly string[] QuantityAliases =
    [
        "quantity",
        "qty"
    ];

    private static readonly string[] NameAliases =
    [
        "name",
        "productname",
        "itemname"
    ];

    private static readonly string[] DescriptionAliases =
    [
        "description",
        "productdescription",
        "itemdescription"
    ];

    private static readonly string[] PriceAliases =
    [
        "price",
        "unitprice",
        "unitamount"
    ];

    private static readonly string[] ProductGroupAliases =
    [
        "productgroup",
        "group",
        "category"
    ];

    public static string? GetOrderId(IReadOnlyDictionary<string, string?> fields) =>
        FirstValue(fields, OrderIdAliases);

    public static OrderSemanticValues GetSemanticValues(
        IReadOnlyDictionary<string, string?> fields)
    {
        return new OrderSemanticValues(
            OrderNumber: FirstValue(fields, OrderNumberAliases),
            CustomerId: FirstValue(fields, CustomerIdAliases),
            CustomerName: FirstValue(fields, CustomerNameAliases),
            Status: FirstValue(fields, StatusAliases),
            CreatedAt: FirstValue(fields, CreatedAtAliases),
            UpdatedAt: FirstValue(fields, UpdatedAtAliases),
            TotalAmount: TryParseAmount(FirstValue(fields, TotalAmountAliases)),
            Currency: FirstValue(fields, CurrencyAliases));
    }

    public static OrderLineSemanticValues GetLineValues(
        IReadOnlyDictionary<string, string?> fields)
    {
        var quantity = TryParseDecimal(FirstValue(fields, QuantityAliases));
        var unitPrice = TryParseAmount(FirstValue(fields, PriceAliases));

        return new OrderLineSemanticValues(
            LineNumber: FirstValue(fields, LineNumberAliases),
            ProductNumber: FirstValue(fields, ProductNumberAliases),
            Quantity: quantity,
            Name: FirstValue(fields, NameAliases),
            Description: FirstValue(fields, DescriptionAliases),
            UnitPrice: unitPrice,
            ProductGroup: FirstValue(fields, ProductGroupAliases),
            LineTotal: quantity.HasValue && unitPrice.HasValue
                ? quantity.Value * unitPrice.Value
                : null);
    }

    public static bool ContainsSearchText(
        IReadOnlyDictionary<string, string?> fields,
        string searchText)
    {
        return fields.Values.Any(value =>
            value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string? FirstValue(
        IReadOnlyDictionary<string, string?> fields,
        IReadOnlyList<string> aliases)
    {
        var normalizedAliases = aliases.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in fields)
        {
            if (value is null)
            {
                continue;
            }

            if (normalizedAliases.Contains(Normalize(key)))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal? TryParseAmount(string? value)
    {
        return TryParseDecimal(value);
    }

    private static decimal? TryParseDecimal(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(
            normalized,
            NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}

internal sealed record OrderSemanticValues(
    string? OrderNumber,
    string? CustomerId,
    string? CustomerName,
    string? Status,
    string? CreatedAt,
    string? UpdatedAt,
    decimal? TotalAmount,
    string? Currency);

internal sealed record OrderLineSemanticValues(
    string? LineNumber,
    string? ProductNumber,
    decimal? Quantity,
    string? Name,
    string? Description,
    decimal? UnitPrice,
    string? ProductGroup,
    decimal? LineTotal)
{
    public bool HasLineData =>
        LineNumber is not null
        || ProductNumber is not null
        || Quantity.HasValue
        || Name is not null
        || Description is not null
        || UnitPrice.HasValue
        || ProductGroup is not null;
}
