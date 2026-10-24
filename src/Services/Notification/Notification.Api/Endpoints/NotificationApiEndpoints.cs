using ShopSphere.Services.Notification.Application.Commands;
using ShopSphere.Services.Notification.Application.DTOs;
using ShopSphere.Services.Notification.Application.Queries;
using ShopSphere.Services.Notification.Domain.Exceptions;

namespace ShopSphere.Services.Notification.Api.Endpoints;

public static class NotificationApiEndpoints
{
    public static IEndpointRouteBuilder MapNotificationApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications");

        group.MapGet("/{id:guid}", async (
            Guid id,
            GetNotificationQueriesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var job = await handler.HandleAsync(new GetNotificationByIdQuery(id), cancellationToken);
            return job is not null
                ? Results.Ok(job)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Notification Job Not Found",
                    detail: $"Notification job '{id}' was not found.");
        })
        .WithName("GetNotificationById")
        .WithSummary("Get a notification job by its identifier.")
        .Produces<NotificationJobDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            GetNotificationQueriesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var jobs = await handler.HandleAsync(new GetNotificationsByOrderIdQuery(orderId), cancellationToken);
            return Results.Ok(jobs);
        })
        .WithName("GetNotificationsByOrderId")
        .WithSummary("Get all notification jobs dispatched for an order.")
        .Produces<IReadOnlyList<NotificationJobDto>>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/retry", async (
            Guid id,
            RetryNotificationCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var dto = await handler.HandleAsync(new RetryNotificationCommand(id), cancellationToken);
                return Results.Ok(dto);
            }
            catch (NotificationJobNotFoundException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Notification Job Not Found",
                    detail: ex.Message);
            }
        })
        .WithName("RetryNotification")
        .WithSummary("Retry dispatching a failed notification job.")
        .Produces<NotificationJobDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
