# Order API

Small ASP.NET Core Web API for browsing order data loaded from CSV files.

## Run

```powershell
dotnet run --urls http://localhost:5000
```

By default the API reads `*.csv` files from `Data/`. The included sample data contains three orders and fourteen order lines. If `Data/` is empty, the API also checks for CSV files in the project root so supplied exercise files can be dropped in without configuration.

You can point the API at another folder with configuration:

```powershell
$env:ORDERDATA__PATH = "C:\path\to\csvs"
dotnet run --urls http://localhost:5000
```

## Endpoints

- `GET /orders?skip=0&take=50&q=&status=` lists orders.
- `GET /orders/{id}` returns one order or `404` with a problem response.
- `GET /data-quality` shows load counts and warnings for dirty CSV rows.

## Data handling

The loader reads every CSV file, detects the order id using common header names such as `OrderId`, `OrderNo`, `OrderNumber`, `OrderReference`, `Order`, or `Id`, and groups rows with the same id into one order.

Known fields such as customer, dates, line number, product number, quantity, name, description, price, and product group are promoted when recognizable. Order totals are calculated from complete line item `Quantity * Price` values. The original CSV values are also preserved in `attributes` and each source row is returned in `records`, so no data is silently lost when an unfamiliar file shape appears.
