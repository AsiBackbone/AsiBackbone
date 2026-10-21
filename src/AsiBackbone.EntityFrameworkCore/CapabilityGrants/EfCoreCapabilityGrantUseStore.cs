using System.Data.Common;
using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Entities;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsiBackbone.EntityFrameworkCore.CapabilityGrants;

/// <summary>Provides durable, atomic capability-grant use control through a host-owned EF Core context.</summary>
public sealed class EfCoreCapabilityGrantUseStore : ICapabilityGrantUseStore
{
    private readonly DbContext dbContext;
    private readonly EfCoreCapabilityGrantUseStoreOptions options;

    /// <summary>Initializes the durable use store.</summary>
    public EfCoreCapabilityGrantUseStore(DbContext dbContext, EfCoreCapabilityGrantUseStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.dbContext = dbContext;
        this.options = options;
    }

    /// <inheritdoc />
    public async ValueTask<CapabilityGrantUseResult> TryConsumeAsync(
        CapabilityGrant grant,
        int maxUseCount,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxUseCount, 1);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset normalizedUsedUtc = usedUtc.ToUniversalTime();
        DateTimeOffset retainUntilUtc = AddClamped(grant.ExpiresUtc, options.RetentionGracePeriod);
        if (normalizedUsedUtc > retainUntilUtc)
        {
            return CapabilityGrantUseResult.RetentionElapsed(
                "The grant is past the EF Core use store's retention horizon.");
        }

        for (int attempt = 0; attempt <= options.ContentionRetryCount; attempt++)
        {
            try
            {
                int affected = await Entries()
                    .Where(entity => entity.Issuer == grant.Issuer
                        && entity.TokenId == grant.TokenId
                        && entity.State == GrantUseState.Accepted
                        && entity.UseCount < maxUseCount)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(entity => entity.UseCount, entity => entity.UseCount + 1)
                        .SetProperty(entity => entity.LastUsedUtc, normalizedUsedUtc)
                        .SetProperty(entity => entity.RetainUntilUtc, retainUntilUtc)
                        .SetProperty(entity => entity.ConcurrencyStamp, GovernanceEntity.NewConcurrencyStamp()), cancellationToken)
                    .ConfigureAwait(false);

                if (affected == 1)
                {
                    int count = await Entries().AsNoTracking()
                        .Where(entity => entity.Issuer == grant.Issuer && entity.TokenId == grant.TokenId)
                        .Select(entity => entity.UseCount)
                        .SingleAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return CapabilityGrantUseResult.Accepted(count);
                }

                CapabilityGrantUseEntity? existing = await Entries().AsNoTracking()
                    .SingleOrDefaultAsync(
                        entity => entity.Issuer == grant.Issuer && entity.TokenId == grant.TokenId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null)
                {
                    return existing.State switch
                    {
                        GrantUseState.Stopped => CapabilityGrantUseResult.Stopped("The durable capability grant is stopped."),
                        GrantUseState.Cancelled => CapabilityGrantUseResult.Cancelled("The durable capability grant is cancelled."),
                        GrantUseState.Accepted or GrantUseState.UseLimitExceeded => CapabilityGrantUseResult.UseLimitExceeded(existing.UseCount),
                        GrantUseState.Unavailable or GrantUseState.Unspecified => CapabilityGrantUseResult.Unavailable(
                            "The durable capability grant use record has an unavailable state."),
                        _ => CapabilityGrantUseResult.Unavailable("The durable capability grant use record has an unknown state.")
                    };
                }

                _ = Entries().Add(new CapabilityGrantUseEntity
                {
                    Id = Guid.NewGuid(),
                    ConcurrencyStamp = GovernanceEntity.NewConcurrencyStamp(),
                    Issuer = grant.Issuer,
                    TokenId = grant.TokenId,
                    UseCount = 1,
                    State = GrantUseState.Accepted,
                    LastUsedUtc = normalizedUsedUtc,
                    RetainUntilUtc = retainUntilUtc
                });
                _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return CapabilityGrantUseResult.Accepted(1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when ((exception is DbUpdateException or DbException) && attempt < options.ContentionRetryCount)
            {
                dbContext.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < options.ContentionRetryCount)
            {
                dbContext.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException or InvalidOperationException or TimeoutException)
            {
                dbContext.ChangeTracker.Clear();
                return CapabilityGrantUseResult.Unavailable("The EF Core capability grant use store could not answer the request.");
            }
        }

        return CapabilityGrantUseResult.Unavailable("The EF Core capability grant use store exhausted contention retries.");
    }

    /// <summary>Marks an issuer-scoped grant as stopped.</summary>
    public ValueTask SetStoppedAsync(
        string issuer,
        string tokenId,
        DateTimeOffset retainUntilUtc,
        CancellationToken cancellationToken = default)
    {
        return SetStateAsync(issuer, tokenId, retainUntilUtc, GrantUseState.Stopped, cancellationToken);
    }

    /// <summary>Marks an issuer-scoped grant as cancelled.</summary>
    public ValueTask SetCancelledAsync(
        string issuer,
        string tokenId,
        DateTimeOffset retainUntilUtc,
        CancellationToken cancellationToken = default)
    {
        return SetStateAsync(issuer, tokenId, retainUntilUtc, GrantUseState.Cancelled, cancellationToken);
    }

    /// <summary>Purges records only after their expiry-plus-skew retention horizon.</summary>
    public async ValueTask<int> PurgeExpiredAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        return await Entries()
            .Where(entity => entity.RetainUntilUtc < utcNow.ToUniversalTime())
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask SetStateAsync(
        string issuer,
        string tokenId,
        DateTimeOffset retainUntilUtc,
        GrantUseState state,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
        cancellationToken.ThrowIfCancellationRequested();

        CapabilityGrantUseEntity? entity = await Entries().SingleOrDefaultAsync(
            item => item.Issuer == issuer.Trim() && item.TokenId == tokenId.Trim(), cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            _ = Entries().Add(new CapabilityGrantUseEntity
            {
                Id = Guid.NewGuid(),
                ConcurrencyStamp = GovernanceEntity.NewConcurrencyStamp(),
                Issuer = issuer.Trim(),
                TokenId = tokenId.Trim(),
                State = state,
                LastUsedUtc = DateTimeOffset.UnixEpoch,
                RetainUntilUtc = retainUntilUtc.ToUniversalTime()
            });
        }
        else
        {
            entity.State = state;
            entity.RetainUntilUtc = retainUntilUtc.ToUniversalTime();
            entity.ConcurrencyStamp = GovernanceEntity.NewConcurrencyStamp();
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private DbSet<CapabilityGrantUseEntity> Entries()
    {
        return dbContext.Set<CapabilityGrantUseEntity>();
    }

    private static DateTimeOffset AddClamped(DateTimeOffset value, TimeSpan duration)
    {
        return value > DateTimeOffset.MaxValue - duration ? DateTimeOffset.MaxValue : value + duration;
    }
}
