using Npgsql;

namespace NotificationCore.IntegrationTests.Infrastructure;

public sealed class ConnectionPoolingIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    private readonly PostgreSqlIntegrationFixture _fixture;

    public ConnectionPoolingIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OpenConnectionAsync_WhenPoolSlotIsReleased_ShouldServeWaitingOperation()
    {
        if (!_fixture.IsAvailable)
            return;

        var builder = new NpgsqlConnectionStringBuilder(_fixture.DatabaseConnectionString)
        {
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 2,
            Timeout = 2,
            ApplicationName = "NotificationCore.PoolingTests"
        };
        await using var dataSource = NpgsqlDataSource.Create(builder.ConnectionString);
        await using var firstConnection = await dataSource.OpenConnectionAsync();
        await using var secondConnection = await dataSource.OpenConnectionAsync();

        var waitingConnectionTask = dataSource.OpenConnectionAsync().AsTask();
        await Task.Delay(100);

        Assert.False(waitingConnectionTask.IsCompleted);

        await firstConnection.DisposeAsync();
        await using var waitingConnection = await waitingConnectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(System.Data.ConnectionState.Open, waitingConnection.State);
    }
}
