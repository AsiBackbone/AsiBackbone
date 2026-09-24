using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox.Providers;

/// <summary>
/// Creates and drops a throwaway database on a real SQL Server or PostgreSQL instance for one contention test.
/// </summary>
/// <remarks>
/// <para>
/// The suite is opt-in. Each provider is enabled by a server-level connection string in an environment variable; the
/// test database name is generated per test, so the configured login needs permission to create and drop databases.
/// On SQL Server, detecting a blocked claim reads <c>sys.dm_exec_requests</c>, which requires <c>VIEW SERVER STATE</c>.
/// </para>
/// <para>
/// When the connection string is absent the test is skipped. Setting <see cref="RequiredVariable" /> to <c>true</c>
/// turns an absent connection string into a failure, so a CI job that is meant to run the suite cannot pass by
/// silently skipping it.
/// </para>
/// </remarks>
internal sealed class ProviderOutboxDatabase(
    OutboxContentionProvider provider,
    string serverConnectionString,
    string databaseName,
    DbContextOptions<GovernanceOutboxTestDbContext> options)
    : IAsyncDisposable
{
    /// <summary>
    /// Environment variable holding a server-level SQL Server connection string.
    /// </summary>
    public const string SqlServerConnectionVariable = "ASIBACKBONE_TEST_SQLSERVER_CONNECTION";

    /// <summary>
    /// Environment variable holding a server-level PostgreSQL connection string.
    /// </summary>
    public const string PostgreSqlConnectionVariable = "ASIBACKBONE_TEST_POSTGRES_CONNECTION";

    /// <summary>
    /// Environment variable that, when <c>true</c>, makes a missing provider connection string fail the test.
    /// </summary>
    public const string RequiredVariable = "ASIBACKBONE_TEST_PROVIDERS_REQUIRED";

    private const string SqlServerBlockedRequestsSql =
        "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id = DB_ID() AND blocking_session_id > 0";

    private const string PostgreSqlLockWaitersSql =
        "SELECT COUNT(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'";

    private const string SqlServerReadCommittedSnapshotSql =
        "SELECT CAST(is_read_committed_snapshot_on AS int) FROM sys.databases WHERE name = DB_NAME()";

    private const string SqlServerEnableReadCommittedSnapshotSql =
        "DECLARE @statement nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@databaseName) " +
        "+ N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE'; EXEC (@statement);";

    private const string SqlServerDisableReadCommittedSnapshotSql =
        "DECLARE @statement nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@databaseName) " +
        "+ N' SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE'; EXEC (@statement);";

    private const string PostgreSqlIsolationSql = "SHOW default_transaction_isolation";

    /// <summary>
    /// Gets the provider configuration this database was created for.
    /// </summary>
    public OutboxContentionProvider Provider { get; } = provider;

    /// <summary>
    /// Gets the options that bind a test context to this database.
    /// </summary>
    public DbContextOptions<GovernanceOutboxTestDbContext> Options { get; } = options;

    /// <summary>
    /// Creates the database for the supplied provider, or skips the test when the provider is not configured.
    /// </summary>
    public static async Task<ProviderOutboxDatabase> CreateAsync(
        OutboxContentionProvider provider,
        CancellationToken cancellationToken)
    {
        string serverConnectionString = RequireServerConnectionString(provider);
        string databaseName = $"asib_outbox_{Guid.NewGuid():N}";

        DbContextOptions<GovernanceOutboxTestDbContext> options = CreateOptions(provider, serverConnectionString, databaseName);
        var database = new ProviderOutboxDatabase(provider, serverConnectionString, databaseName, options);

        try
        {
            await database.InitializeAsync(cancellationToken);
        }
        catch (Exception setupFailure)
        {
            // The caller never receives the object when setup fails, so drop the partially created database here
            // rather than leaving a randomly named database on the shared server.
            try
            {
                await database.DisposeAsync();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    $"Setting up the {provider} test database failed, and dropping it afterward also failed.",
                    setupFailure,
                    cleanupFailure);
            }

            throw;
        }

        return database;
    }

    /// <summary>
    /// Creates a new, independent context bound to this database.
    /// </summary>
    public GovernanceOutboxTestDbContext CreateContext()
    {
        return new(Options);
    }

    /// <summary>
    /// Counts sessions in this database that are currently waiting on a lock held by another session.
    /// </summary>
    public async Task<int> CountLockWaitingSessionsAsync(CancellationToken cancellationToken)
    {
        object? value = await ExecuteScalarAsync(ProbeQuery.LockWaiters, cancellationToken);

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        NpgsqlConnection.ClearAllPools();

        await using GovernanceOutboxTestDbContext context = CreateContext();
        _ = await context.Database.EnsureDeletedAsync();
    }

    private static string RequireServerConnectionString(OutboxContentionProvider provider)
    {
        string variable = provider is OutboxContentionProvider.PostgreSql
            ? PostgreSqlConnectionVariable
            : SqlServerConnectionVariable;

        string? value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string message = $"Set {variable} to a server-level connection string to run the {provider} outbox contention tests.";
        bool required = string.Equals(
            Environment.GetEnvironmentVariable(RequiredVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (required)
        {
            Assert.Fail(message + $" {RequiredVariable} is true, so the suite must not be skipped.");
        }
        else
        {
            Assert.Skip(message);
        }

        // Assert.Fail and Assert.Skip both throw; this keeps the method's contract independent of their annotations.
        throw new InvalidOperationException(message);
    }

    private static DbContextOptions<GovernanceOutboxTestDbContext> CreateOptions(
        OutboxContentionProvider provider,
        string serverConnectionString,
        string databaseName)
    {
        DbContextOptionsBuilder<GovernanceOutboxTestDbContext> builder = new();

        return provider switch
        {
            OutboxContentionProvider.SqlServerLockingReadCommitted or OutboxContentionProvider.SqlServerReadCommittedSnapshot =>
                builder.UseSqlServer(
                    new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = databaseName }.ConnectionString)
                .Options,
            OutboxContentionProvider.PostgreSql =>
                builder.UseNpgsql(
                    new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = databaseName }.ConnectionString)
                .Options,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported contention provider."),
        };
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using (GovernanceOutboxTestDbContext context = CreateContext())
        {
            _ = await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        // Set the snapshot mode explicitly for both SQL Server configurations rather than relying on the database
        // default, which depends on the server and its model database and was observed to be ON in CI.
        if (Provider is OutboxContentionProvider.SqlServerLockingReadCommitted or OutboxContentionProvider.SqlServerReadCommittedSnapshot)
        {
            await SetReadCommittedSnapshotAsync(
                enabled: Provider is OutboxContentionProvider.SqlServerReadCommittedSnapshot,
                cancellationToken);
        }

        await AssertIsolationPreconditionAsync(cancellationToken);
    }

    private async Task SetReadCommittedSnapshotAsync(bool enabled, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(serverConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand command = connection.CreateCommand();

        // Both branches are constants, so the command text never carries caller-supplied input.
        command.CommandText = enabled
            ? SqlServerEnableReadCommittedSnapshotSql
            : SqlServerDisableReadCommittedSnapshotSql;

        _ = command.Parameters.AddWithValue("@databaseName", databaseName);
        _ = await command.ExecuteNonQueryAsync(cancellationToken);

        // Connections pooled before the change keep no stale session state, but clearing them keeps later contexts
        // from reusing a connection that ROLLBACK IMMEDIATE terminated.
        SqlConnection.ClearAllPools();
    }

    private async Task AssertIsolationPreconditionAsync(CancellationToken cancellationToken)
    {
        switch (Provider)
        {
            case OutboxContentionProvider.SqlServerLockingReadCommitted:
                Assert.Equal(0, Convert.ToInt32(
                    await ExecuteScalarAsync(ProbeQuery.ReadCommittedSnapshot, cancellationToken),
                    CultureInfo.InvariantCulture));
                break;
            case OutboxContentionProvider.SqlServerReadCommittedSnapshot:
                Assert.Equal(1, Convert.ToInt32(
                    await ExecuteScalarAsync(ProbeQuery.ReadCommittedSnapshot, cancellationToken),
                    CultureInfo.InvariantCulture));
                break;
            case OutboxContentionProvider.PostgreSql:
                Assert.Equal(
                    "read committed",
                    Convert.ToString(await ExecuteScalarAsync(ProbeQuery.Isolation, cancellationToken), CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"Unsupported contention provider {Provider}.");
        }
    }

    private async Task<object?> ExecuteScalarAsync(ProbeQuery query, CancellationToken cancellationToken)
    {
        await using GovernanceOutboxTestDbContext context = CreateContext();
        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        await using DbCommand command = connection.CreateCommand();
        bool postgreSql = Provider is OutboxContentionProvider.PostgreSql;

        // Each branch assigns a constant so the command text never carries caller-supplied input.
        command.CommandText = query switch
        {
            ProbeQuery.LockWaiters when postgreSql => PostgreSqlLockWaitersSql,
            ProbeQuery.LockWaiters => SqlServerBlockedRequestsSql,
            ProbeQuery.ReadCommittedSnapshot => SqlServerReadCommittedSnapshotSql,
            ProbeQuery.Isolation => PostgreSqlIsolationSql,
            _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unsupported probe query."),
        };
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private enum ProbeQuery
    {
        LockWaiters,
        ReadCommittedSnapshot,
        Isolation,
    }
}
