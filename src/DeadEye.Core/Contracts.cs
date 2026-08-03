namespace DeadEye.Core;

public enum TransactionState { Draft, Evaluated, Previewed, Consented, Prepared, Applying, Verifying, Committed, RollingBack, RolledBack, RecoveryRequired, Conflict }
public enum ValidationStatus { Unknown, Passed, Failed, Conflict }
public enum RollbackStatus { NotRequired, Pending, Succeeded, Failed, RecoveryRequired }

public sealed record TargetFingerprint(string TargetId, string Owner, string StateHash);
public sealed record TypedAction(string ActionId, string Version, string TargetId, string Owner, string PreviousValue, string NewValue, IReadOnlyList<string> Dependencies);
public sealed record ActionPlan(string ActionId, string Version, string Module, IReadOnlyList<TypedAction> Actions, string UserSid, string SessionId, DateTimeOffset CreatedAtUtc)
{
    public string Hash => PlanHasher.Compute(this);
}
public sealed record ActionResult(string ActionId, ValidationStatus Validation, string Message);
public sealed record TransactionRecord(Guid Id, TransactionState State, ValidationStatus Validation, RollbackStatus Rollback, string PlanHash, DateTimeOffset TimestampUtc, IReadOnlyList<ActionResult> Results);

public interface IActionAdapter
{
    string ActionId { get; }
    Task<string> ReadStateAsync(TypedAction action, CancellationToken ct);
    Task BackupAsync(TypedAction action, CancellationToken ct);
    Task ApplyAsync(TypedAction action, CancellationToken ct);
    Task<bool> VerifyAsync(TypedAction action, CancellationToken ct);
    Task RollbackAsync(TypedAction action, CancellationToken ct);
}
public interface ITransactionStore
{
    Task AppendAsync(Guid id, TransactionState state, string eventName, string payload, CancellationToken ct);
    Task<TransactionState?> GetStateAsync(Guid id, CancellationToken ct);
}
public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
