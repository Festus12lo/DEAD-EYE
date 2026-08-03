namespace DeadEye.Core;

public enum OptimizationCategory { Battery, Performance, Gaming, Network, Storage, Memory, Thermal, Windows }
public enum OptimizationDisposition { Supported, Unsupported, Blocked, Experimental, ReadOnly }
public enum BatteryOptimizationDisposition { Supported, Unsupported, Blocked, Experimental, Unsafe }
public sealed record RollbackInformation(string Strategy, string Coverage, bool VerifiedBeforeApply);
public sealed record OptimizationDependency(string Key, string Description, bool Required);
public sealed record OptimizationDefinition(string OptimizationId, string Version, string Name, OptimizationCategory Category, IReadOnlyCollection<OptimizationDependency> Dependencies, RollbackInformation Rollback, string EvidenceReference, RiskLevel Risk, OptimizationDisposition Disposition, string Explanation);
public sealed record OptimizationContext(IReadOnlyCollection<ValidationFact> Facts, IReadOnlyCollection<Observation<object>> Observations, bool DryRun);
public sealed record BatteryOptimizationAssessment(string OptimizationId, string Title, BatteryOptimizationDisposition Disposition, string Explanation, EvidenceRecord? Evidence, RiskLevel Risk, RollbackInformation Rollback, IReadOnlyList<string> Dependencies, string ExpectedBatteryGain, string ExpectedCpuReduction, string ExpectedWakeTimerReduction, string ExpectedDiskReduction, decimal ConfidenceScore, string BenchmarkPrediction, IReadOnlyList<string> Warnings);
public sealed record BatteryIntelligenceReport(DateTimeOffset CreatedAtUtc, IReadOnlyList<BatteryOptimizationAssessment> Assessments, IReadOnlyList<string> Unknowns, bool MutationAvailable);
public interface IEvidenceCatalog { bool TryGet(string id, out EvidenceRecord? record); }
public sealed class EvidenceCatalog : IEvidenceCatalog
{
    private readonly Dictionary<string, EvidenceRecord> _records;
    public EvidenceCatalog(IEnumerable<EvidenceRecord> records) => _records = records.ToDictionary(record => record.EvidenceId, StringComparer.Ordinal);
    public bool TryGet(string id, out EvidenceRecord? record) => _records.TryGetValue(id, out record);
}
public sealed class BatteryIntelligenceEngine
{
    private readonly IReadOnlyList<BatteryOptimizationAssessment> _catalog;
    private readonly IEvidenceCatalog _evidence;
    public BatteryIntelligenceEngine(IEnumerable<BatteryOptimizationAssessment> catalog, IEvidenceCatalog evidence) { _catalog = catalog.ToArray(); _evidence = evidence; }
    public Task<BatteryIntelligenceReport> AnalyzeAsync(OptimizationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var unknowns = context.Observations.Where(o => o.Status != ObservationStatus.Available || o.Value is null).Select(o => o.Key).Distinct().ToArray();
        var assessments = _catalog.Select(candidate =>
        {
            _evidence.TryGet(candidate.OptimizationId, out var evidence);
            var disposition = evidence is null || !EvidencePolicy.IsEligible(evidence) ? BatteryOptimizationDisposition.Blocked : candidate.Disposition;
            var warnings = candidate.Warnings.ToList();
            if (disposition == BatteryOptimizationDisposition.Blocked) warnings.Add("Evidence missing or ineligible");
            return candidate with { Evidence = evidence, Disposition = disposition, Warnings = warnings.Distinct().ToArray() };
        }).ToArray();
        return Task.FromResult(new BatteryIntelligenceReport(DateTimeOffset.UtcNow, assessments, unknowns, false));
    }
}
