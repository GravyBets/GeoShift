using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class MigrationCorrectionEngineTests
{
    [Fact]
    public void ApprovedCorrectionChangesExportOnlyAndPreservesAudit()
    {
        var source = new Dictionary<string, string?> { ["Site"] = "102", ["RadioSN"] = " ABC " };
        var resolved = new ConflictResolutionResult([new ResolvedDatasetRow(4, "Inventory.csv", 3, source)], []);
        var corrected = MigrationCorrectionEngine.Apply(resolved, ["Site", "RadioSN"],
            [new MigrationCorrection(4, "RadioSN", " ABC ", "ABC")]);
        Assert.Equal(" ABC ", source["RadioSN"]);
        Assert.Equal("ABC", corrected.Resolved.Rows[0].Columns["RadioSN"]);
        var audit = Assert.Single(corrected.Audit);
        Assert.Equal(" ABC ", audit.OriginalValue);
        Assert.Equal("ABC", audit.CorrectedValue);
    }

    [Fact]
    public void RejectsStaleOrUnknownCorrections()
    {
        var resolved = new ConflictResolutionResult(
            [new ResolvedDatasetRow(0, "A.csv", 1, new Dictionary<string, string?> { ["Serial"] = "A" })], []);
        Assert.Throws<InvalidDataException>(() => MigrationCorrectionEngine.Apply(resolved, ["Serial"],
            [new MigrationCorrection(0, "Serial", "B", "C")]));
        Assert.Throws<InvalidDataException>(() => MigrationCorrectionEngine.Apply(resolved, ["Serial"],
            [new MigrationCorrection(1, "Serial", "A", "C")]));
    }

    [Fact]
    public void CorrectedValuesAreRevalidated()
    {
        var resolved = new ConflictResolutionResult(
            [new ResolvedDatasetRow(0, "A.csv", 1, new Dictionary<string, string?> { ["Serial"] = "" })], []);
        var before = MigrationValidationEngine.Validate(resolved, ["Serial"], ["Serial"]);
        var after = MigrationCorrectionEngine.Apply(resolved, ["Serial"],
            [new MigrationCorrection(0, "Serial", "", "ABC")]);
        Assert.Equal(1, before.ErrorCount);
        Assert.Equal(0, MigrationValidationEngine.Validate(after.Resolved, ["Serial"], ["Serial"]).ErrorCount);
    }
}
