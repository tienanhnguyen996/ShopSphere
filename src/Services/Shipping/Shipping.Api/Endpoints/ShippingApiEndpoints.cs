using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Shipping.Application.Commands;
using ShopSphere.Services.Shipping.Application.DTOs;
using ShopSphere.Services.Shipping.Application.Queries;
using ShopSphere.Services.Shipping.Domain.Exceptions;

namespace ShopSphere.Services.Shipping.Api.Endpoints;

public sealed record RecordHandoverRequestDto(
    string WarehouseLocation,
    string Notes);

public sealed record RecordDeliveryRequestDto(
    string Location,
    string Notes);

public sealed record CancelShipmentRequestDto(
    string Reason);

public static class ShippingApiEndpoints
{
    public static IEndpointRouteBuilder MapShippingApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shipments")
            .WithTags("Shipments");

        group.MapGet("/{shipmentId:guid}", async (
            Guid shipmentId,
            GetShipmentQueriesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var shipment = await handler.HandleAsync(new GetShipmentByIdQuery(shipmentId), cancellationToken);
            return shipment is not null
                ? Results.Ok(shipment)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Shipment Not Found",
                    detail: $"Shipment '{shipmentId}' was not found.");
        })
        .WithName("GetShipmentById")
        .WithSummary("Get a shipment by its identifier.")
        .Produces<ShipmentDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            GetShipmentQueriesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var shipment = await handler.HandleAsync(new GetShipmentByOrderIdQuery(orderId), cancellationToken);
            return shipment is not null
                ? Results.Ok(shipment)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Shipment Not Found",
                    detail: $"Shipment for order '{orderId}' was not found.");
        })
        .WithName("GetShipmentByOrderId")
        .WithSummary("Get a shipment by order identifier.")
        .Produces<ShipmentDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/tracking/{trackingNumber}", async (
            string trackingNumber,
            GetShipmentQueriesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var shipment = await handler.HandleAsync(new GetShipmentByTrackingNumberQuery(trackingNumber), cancellationToken);
            return shipment is not null
                ? Results.Ok(shipment)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Shipment Not Found",
                    detail: $"Shipment with tracking number '{trackingNumber}' was not found.");
        })
        .WithName("GetShipmentByTrackingNumber")
        .WithSummary("Get shipment tracking details and milestones by tracking number.")
        .Produces<ShipmentDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{shipmentId:guid}/handover", async (
            Guid shipmentId,
            [FromBody] RecordHandoverRequestDto request,
            HttpContext httpContext,
            RecordCarrierHandoverCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
                var result = await handler.HandleAsync(
                    new RecordCarrierHandoverCommand(shipmentId, request.WarehouseLocation, request.Notes, correlationId),
                    cancellationToken);
                return Results.Ok(result);
            }
            catch (ShipmentNotFoundException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Shipment Not Found",
                    detail: ex.Message);
            }
            catch (InvalidShipmentStateTransitionException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Invalid Shipment State Transition",
                    detail: ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Concurrency Conflict",
                    detail: "Shipment was modified concurrently. Please retry.");
            }
        })
        .WithName("RecordCarrierHandover")
        .WithSummary("Record physical carrier handover at the warehouse and emit ShipmentHandedOver.")
        .Produces<ShipmentDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{shipmentId:guid}/deliver", async (
            Guid shipmentId,
            [FromBody] RecordDeliveryRequestDto request,
            HttpContext httpContext,
            RecordDeliveryCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
                var result = await handler.HandleAsync(
                    new RecordDeliveryCommand(shipmentId, request.Location, request.Notes, correlationId),
                    cancellationToken);
                return Results.Ok(result);
            }
            catch (ShipmentNotFoundException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Shipment Not Found",
                    detail: ex.Message);
            }
            catch (InvalidShipmentStateTransitionException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Invalid Shipment State Transition",
                    detail: ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Concurrency Conflict",
                    detail: "Shipment was modified concurrently. Please retry.");
            }
        })
        .WithName("RecordShipmentDelivery")
        .WithSummary("Record final package delivery to the customer.")
        .Produces<ShipmentDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{shipmentId:guid}/cancel", async (
            Guid shipmentId,
            [FromBody] CancelShipmentRequestDto request,
            CancelShipmentCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await handler.HandleAsync(
                    new CancelShipmentCommand(shipmentId, request.Reason),
                    cancellationToken);
                return Results.Ok(result);
            }
            catch (ShipmentNotFoundException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Shipment Not Found",
                    detail: ex.Message);
            }
            catch (InvalidShipmentStateTransitionException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Invalid Shipment State Transition",
                    detail: ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Concurrency Conflict",
                    detail: "Shipment was modified concurrently. Please retry.");
            }
        })
        .WithName("CancelShipment")
        .WithSummary("Cancel a shipment prior to carrier physical handover.")
        .Produces<ShipmentDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
