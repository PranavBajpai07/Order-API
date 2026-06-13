using Logistics.OrderApi.Domain;
using Logistics.OrderApi.Infrastructure.Csv;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OrderDataOptions>(
    builder.Configuration.GetSection(OrderDataOptions.SectionName));
builder.Services.AddSingleton<IOrderRepository, CsvOrderRepository>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "Order API",
    endpoints = new[]
    {
        "GET /orders?skip=0&take=50&q=&status=",
        "GET /orders/{id}",
        "GET /data-quality"
    }
}));

app.MapGet("/orders", (
    IOrderRepository repository,
    int? skip,
    int? take,
    string? q,
    string? status) =>
{
    var requestedSkip = skip ?? 0;
    var requestedTake = take ?? 50;
    var errors = new Dictionary<string, string[]>();

    if (requestedSkip < 0)
    {
        errors["skip"] = ["Skip must be zero or greater."];
    }

    if (requestedTake <= 0)
    {
        errors["take"] = ["Take must be greater than zero."];
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var result = repository.Browse(new OrderBrowseRequest(
        requestedSkip,
        requestedTake,
        q,
        status));

    return Results.Ok(result);
});

app.MapGet("/orders/{id}", (IOrderRepository repository, string id) =>
{
    if (string.IsNullOrWhiteSpace(id))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["id"] = ["Order id is required."]
        });
    }

    var order = repository.GetById(id);
    if (order is null)
    {
        return Results.NotFound(new ProblemDetails
        {
            Title = "Order not found",
            Detail = $"No order with id '{id}' exists in the loaded CSV data.",
            Status = StatusCodes.Status404NotFound
        });
    }

    return Results.Ok(order);
});

app.MapGet("/data-quality", (IOrderRepository repository) =>
    Results.Ok(repository.Diagnostics));

app.Run();

public partial class Program;
