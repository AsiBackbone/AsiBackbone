using AsiBackbone.Core.Entities;
using Xunit;

namespace AsiBackbone.Core.Tests.Entities;

/// <summary>
/// Contains unit tests for the GovernanceEntity class, ensuring that its properties and methods behave as expected, including initialization of Id and ConcurrencyStamp, implementation of core interfaces, and generation of unique concurrency stamps.
/// </summary>
public sealed class AsiBackboneEntityTests
{
    /// <summary>
    /// Verifies that the constructor of GovernanceEntity initializes the Id property with a non-empty GUID.
    /// </summary>
    [Fact]
    public void ConstructorInitializesId()
    {
        TestAsiBackboneEntity entity = new();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    /// <summary>
    /// Verifies that the constructor of GovernanceEntity initializes the ConcurrencyStamp property with a non-empty string of length 32.
    /// </summary>
    [Fact]
    public void ConstructorInitializesConcurrencyStamp()
    {
        TestAsiBackboneEntity entity = new();

        Assert.False(string.IsNullOrWhiteSpace(entity.ConcurrencyStamp));
        Assert.Equal(32, entity.ConcurrencyStamp.Length);
    }

    /// <summary>
    /// Verifies that the TestAsiBackboneEntity class implements the IGovernanceEntity and IConcurrencyTrackedEntity interfaces, ensuring it adheres to the expected contracts for entities in the ASI Backbone system.
    /// </summary>
    [Fact]
    public void EntityImplementsCoreContracts()
    {
        TestAsiBackboneEntity entity = new();

        _ = Assert.IsType<IGovernanceEntity>(entity, exactMatch: false);
        _ = Assert.IsType<IConcurrencyTrackedEntity>(entity, exactMatch: false);
    }

    /// <summary>
    /// Verifies that the NewConcurrencyStamp method generates unique, normalized values that are 32 characters long and do not contain hyphens, ensuring that each concurrency stamp is suitable for use in concurrency tracking without formatting issues.
    /// </summary>
    [Fact]
    public void NewConcurrencyStampReturnsUniqueNormalizedValues()
    {
        string first = GovernanceEntity.NewConcurrencyStamp();
        string second = GovernanceEntity.NewConcurrencyStamp();

        Assert.NotEqual(first, second);
        Assert.Equal(32, first.Length);
        Assert.Equal(32, second.Length);
        Assert.DoesNotContain("-", first, StringComparison.Ordinal);
        Assert.DoesNotContain("-", second, StringComparison.Ordinal);
    }

    private sealed class TestAsiBackboneEntity : GovernanceEntity
    {
    }
}
