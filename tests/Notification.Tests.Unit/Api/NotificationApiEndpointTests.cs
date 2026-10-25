using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShopSphere.Services.Notification.Api.Endpoints;
using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Application.Commands;
using ShopSphere.Services.Notification.Application.DTOs;
using ShopSphere.Services.Notification.Application.Queries;
using ShopSphere.Services.Notification.Domain;
using ShopSphere.Services.Notification.Infrastructure.Senders;
using ShopSphere.Services.Notification.Tests.Unit.Helpers;

namespace ShopSphere.Services.Notification.Tests.Unit.Api;

public class NotificationApiEndpointTests
{
    [Fact]
    public async Task GetAndRetryEndpoints_ReturnExpectedNotificationJobs()
    {
        var orderId = Guid.NewGuid();
        var job = NotificationJob.Create(
            orderId,
            "cust-200",
            NotificationType.OrderConfirmed,
            NotificationChannel.Email,
            "cust-200@customers.shopsphere.local",
            "Order Confirmed",
            "Confirmed body",
            Guid.NewGuid());
        job.RecordAttemptFailure("Temporary SMTP failure");

        var store = new List<NotificationJob> { job };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(store);
        var dbMock = new Mock<INotificationDbContext>();
        dbMock.SetupGet(d => d.NotificationJobs).Returns(mockSet.Object);
        dbMock.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(dbMock.Object);
        builder.Services.AddSingleton<INotificationSender, MockNotificationSender>();
        builder.Services.AddScoped<RetryNotificationCommandHandler>();
        builder.Services.AddScoped<GetNotificationQueriesHandler>();

        await using var app = builder.Build();
        app.MapNotificationApiEndpoints();
        await app.StartAsync();

        using var client = app.GetTestClient();

        // 1. Get by OrderId
        var listResp = await client.GetAsync($"/api/v1/notifications/orders/{orderId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var jobs = await listResp.Content.ReadFromJsonAsync<List<NotificationJobDto>>();
        jobs.Should().ContainSingle(j => j.Id == job.Id && j.Status == "Failed");

        // 2. Retry failed notification job
        var retryResp = await client.PostAsync($"/api/v1/notifications/{job.Id}/retry", null);
        retryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var retriedDto = await retryResp.Content.ReadFromJsonAsync<NotificationJobDto>();
        retriedDto!.Status.Should().Be("Sent");

        await app.StopAsync();
    }
}
