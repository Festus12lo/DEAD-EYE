using Xunit;
using DeadEye.Core;
namespace DeadEye.Core.Tests;
public sealed class CanonicalBaselineTests
{
    [Fact]
    public async Task Missing_fact_is_unknown()
    {
        var report = await new ValidationEngine().EvaluateAsync(Array.Empty<ValidationFact>(), new[] { new ValidationRule("x", "x", "missing", RuleOperator.Equals, "yes", ValidationDisposition.Blocked) });
        Assert.Equal(ValidationDisposition.Unknown, report.OverallDisposition);
    }
    [Fact]
    public async Task Battery_report_is_preview_only()
    {
        var engine = new BatteryIntelligenceEngine(Array.Empty<BatteryOptimizationAssessment>(), new EvidenceCatalog(Array.Empty<EvidenceRecord>()));
        var report = await engine.AnalyzeAsync(new OptimizationContext(Array.Empty<ValidationFact>(), Array.Empty<Observation<object>>(), true));
        Assert.False(report.MutationAvailable);
    }
}
