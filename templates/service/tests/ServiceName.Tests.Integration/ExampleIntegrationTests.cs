namespace ShopSphere.Services.ServiceName.Tests.Integration;

public class ExampleIntegrationTests : IClassFixture<ServiceNameWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ExampleIntegrationTests(ServiceNameWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Example_HappyPath_ReturnsSuccess()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/api/v1/health");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
