namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox.Providers;

/// <summary>
/// Real relational providers and isolation configurations exercised by the opt-in outbox contention suite.
/// </summary>
/// <remarks>
/// SQL Server is covered twice because its default read committed isolation behaves differently depending on the
/// database <c>READ_COMMITTED_SNAPSHOT</c> setting: statements either take shared locks or read row versions, and a
/// claim that reads a stale version is exactly the interleaving the contention tests need to rule out.
/// </remarks>
public enum OutboxContentionProvider
{
    /// <summary>
    /// SQL Server with <c>READ_COMMITTED_SNAPSHOT OFF</c>, set explicitly on the test database.
    /// </summary>
    SqlServerLockingReadCommitted,

    /// <summary>
    /// SQL Server with <c>READ_COMMITTED_SNAPSHOT ON</c>, set explicitly on the test database; the Azure SQL Database default.
    /// </summary>
    SqlServerReadCommittedSnapshot,

    /// <summary>
    /// PostgreSQL at its default read committed isolation.
    /// </summary>
    PostgreSql,
}
