namespace DeadEye.Core;

public sealed class TransactionKernel
{
    private readonly ITransactionStore _store;
    private readonly IClock _clock;
    private readonly IReadOnlyDictionary<string, IActionAdapter> _adapters;

    public TransactionKernel(ITransactionStore store, IClock? clock, IEnumerable<IActionAdapter> adapters)
    {
        _store = store;
        _clock = clock ?? new SystemClock();
        _adapters = adapters.ToDictionary(a => a.ActionId, StringComparer.Ordinal);
    }

    public async Task<TransactionRecord> ExecuteAsync(ActionPlan plan, IReadOnlyDictionary<string, TargetFingerprint> expected, bool consented, CancellationToken ct = default)
    {
        if (!consented) throw new InvalidOperationException("Consent required");
        if (plan.Actions.Count == 0) throw new InvalidOperationException("Empty plan");

        var id = Guid.NewGuid();
        var prepared = new List<TypedAction>();
        var results = new List<ActionResult>();
        await TransitionAsync(id, TransactionState.Draft, "PlanFrozen", plan.Hash, ct);
        await TransitionAsync(id, TransactionState.Evaluated, "Evaluated", "typed", ct);
        await TransitionAsync(id, TransactionState.Previewed, "Previewed", "rollback-required", ct);
        await TransitionAsync(id, TransactionState.Consented, "Consented", plan.UserSid + ":" + plan.SessionId, ct);

        try
        {
            foreach (var action in plan.Actions)
            {
                if (!_adapters.ContainsKey(action.ActionId) || !expected.ContainsKey(action.TargetId))
                    throw new InvalidOperationException("Unknown action or fingerprint");
                var current = await _adapters[action.ActionId].ReadStateAsync(action, ct);
                if (!string.Equals(current, expected[action.TargetId].StateHash, StringComparison.Ordinal))
                    return await ConflictAsync(id, plan, "Target changed", ct);
                await _adapters[action.ActionId].BackupAsync(action, ct);
                prepared.Add(action);
            }

            await TransitionAsync(id, TransactionState.Prepared, "Prepared", prepared.Count.ToString(), ct);
            await TransitionAsync(id, TransactionState.Applying, "Applying", "armed", ct);
            foreach (var action in prepared) await _adapters[action.ActionId].ApplyAsync(action, ct);
            await TransitionAsync(id, TransactionState.Verifying, "Verifying", "post-apply", ct);
            foreach (var action in prepared)
            {
                var verified = await _adapters[action.ActionId].VerifyAsync(action, ct);
                results.Add(new ActionResult(action.ActionId, verified ? ValidationStatus.Passed : ValidationStatus.Failed, verified ? "Verified" : "Failed"));
                if (!verified) throw new InvalidOperationException("Verification failed");
            }
            await TransitionAsync(id, TransactionState.Committed, "Committed", "verified", ct);
            return new TransactionRecord(id, TransactionState.Committed, ValidationStatus.Passed, RollbackStatus.NotRequired, plan.Hash, _clock.UtcNow, results);
        }
        catch (Exception exception)
        {
            await TransitionAsync(id, TransactionState.RollingBack, "RollbackStarted", exception.Message, CancellationToken.None);
            for (var index = prepared.Count - 1; index >= 0; index--)
            {
                try { await _adapters[prepared[index].ActionId].RollbackAsync(prepared[index], CancellationToken.None); }
                catch (Exception rollbackException)
                {
                    await TransitionAsync(id, TransactionState.RecoveryRequired, "RollbackFailed", rollbackException.Message, CancellationToken.None);
                    return new TransactionRecord(id, TransactionState.RecoveryRequired, ValidationStatus.Failed, RollbackStatus.RecoveryRequired, plan.Hash, _clock.UtcNow, results);
                }
            }
            await TransitionAsync(id, TransactionState.RolledBack, "RolledBack", exception.Message, CancellationToken.None);
            return new TransactionRecord(id, TransactionState.RolledBack, ValidationStatus.Failed, RollbackStatus.Succeeded, plan.Hash, _clock.UtcNow, results);
        }
    }

    private async Task<TransactionRecord> ConflictAsync(Guid id, ActionPlan plan, string message, CancellationToken ct)
    {
        await TransitionAsync(id, TransactionState.Conflict, "Conflict", message, ct);
        return new TransactionRecord(id, TransactionState.Conflict, ValidationStatus.Conflict, RollbackStatus.NotRequired, plan.Hash, _clock.UtcNow, Array.Empty<ActionResult>());
    }

    private Task TransitionAsync(Guid id, TransactionState state, string eventName, string payload, CancellationToken ct)
        => _store.AppendAsync(id, state, eventName, payload, ct);
}
