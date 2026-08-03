namespace DeadEye.Core;

public enum EvidenceClass { Direct, Instrumented, Proxy, Documentation, Community, Unknown }
public enum RiskLevel { Low, Medium, High, Blocked }
public enum ValidationDisposition { Approved, ApprovedWithWarning, ReadOnlyRecommendation, Blocked, Unknown }
public enum RuleOperator { Equals, NotEquals, Contains, Exists }
public sealed record EvidenceRecord(string EvidenceId, string Title, string TechnicalDescription, IReadOnlyList<string> MicrosoftReferences, IReadOnlyList<string> OemReferences, IReadOnlyList<string> AcademicReferences, IReadOnlyList<string> CommunityReferences, decimal ConfidenceScore, EvidenceClass EvidenceClass, string ExpectedImprovement, string ValidationProcedure, string RollbackProcedure, RiskLevel RiskLevel);
public sealed record ValidationFact(string Key, object? Value, string Source, ObservationConfidence Confidence);
public sealed record ValidationRule(string RuleId, string Description, string FactKey, RuleOperator Operator, string? ExpectedValue, ValidationDisposition FailureDisposition);
public sealed record ValidationFinding(string RuleId, string Description, ValidationDisposition Disposition, string? Detail);
public sealed record ValidationReport(DateTimeOffset CreatedAtUtc, IReadOnlyList<ValidationFinding> Findings, ValidationDisposition OverallDisposition);
public sealed class ValidationEngine
{
    public Task<ValidationReport> EvaluateAsync(IReadOnlyCollection<ValidationFact> facts, IReadOnlyCollection<ValidationRule> rules, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var findings = rules.Select(rule =>
        {
            var fact = facts.LastOrDefault(value => value.Key == rule.FactKey);
            if (fact is null || fact.Confidence == ObservationConfidence.Unknown) return new ValidationFinding(rule.RuleId, rule.Description, ValidationDisposition.Unknown, "Fact unavailable");
            var actual = Convert.ToString(fact.Value) ?? string.Empty;
            var matches = rule.Operator switch
            {
                RuleOperator.Exists => fact.Value is not null,
                RuleOperator.Equals => string.Equals(actual, rule.ExpectedValue, StringComparison.OrdinalIgnoreCase),
                RuleOperator.NotEquals => !string.Equals(actual, rule.ExpectedValue, StringComparison.OrdinalIgnoreCase),
                RuleOperator.Contains => actual.Contains(rule.ExpectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
            return new ValidationFinding(rule.RuleId, rule.Description, matches ? ValidationDisposition.Approved : rule.FailureDisposition, matches ? null : actual);
        }).ToArray();
        var overall = findings.Any(f => f.Disposition == ValidationDisposition.Blocked) ? ValidationDisposition.Blocked : findings.Any(f => f.Disposition == ValidationDisposition.Unknown) ? ValidationDisposition.Unknown : ValidationDisposition.Approved;
        return Task.FromResult(new ValidationReport(DateTimeOffset.UtcNow, findings, overall));
    }
}
public static class EvidencePolicy
{
    public static bool IsEligible(EvidenceRecord record) => record.ConfidenceScore >= 0 && record.ConfidenceScore <= 1 && record.EvidenceClass != EvidenceClass.Unknown && record.RiskLevel != RiskLevel.Blocked && record.MicrosoftReferences.Count + record.OemReferences.Count + record.AcademicReferences.Count + record.CommunityReferences.Count > 0 && !string.IsNullOrWhiteSpace(record.ValidationProcedure) && !string.IsNullOrWhiteSpace(record.RollbackProcedure);
}
