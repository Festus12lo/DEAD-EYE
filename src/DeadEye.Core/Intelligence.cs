namespace DeadEye.Core;

public enum ObservationConfidence { Unknown, Low, Medium, High }
public enum ObservationStatus { Available, Unavailable, Failed, Stale }
public sealed record Observation<T>(string Key, T? Value, string Source, ObservationConfidence Confidence, DateTimeOffset ObservedAtUtc, string Provider, ObservationStatus Status, string? FailureReason = null);
public sealed record ProviderResult(string Provider, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, IReadOnlyList<Observation<object>> Observations, ObservationStatus Status, string? FailureReason = null);
public interface ISystemProvider { string ProviderId { get; } IReadOnlyCollection<string> Capabilities { get; } Task<ProviderResult> CollectAsync(CancellationToken ct = default); }
