using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class MigrationValidationEngineTests
{
    [Fact]
    public void ReportsRequiredValuesAndFormattingWithoutChangingRows()
    {
        var original = new Dictionary<string, string?> { ["Site"] = "102", ["RadioSN"] = " ABC123 ", ["CyberLockSN"] = "" };
        var resolved = new ConflictResolutionResult(
            [new ResolvedDatasetRow(0, "Inventory.csv", 2, original)], []);
        var result = MigrationValidationEngine.Validate(resolved,
            ["Site", "RadioSN", "CyberLockSN"], ["CyberLockSN"]);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Contains(result.Issues, x => x.Column == "CyberLockSN" && x.SourceRowNumber == 2 &&
            x.Severity == ValidationSeverity.Error);
        Assert.Contains(result.Issues, x => x.Column == "RadioSN" && x.Severity == ValidationSeverity.Warning);
        Assert.Equal(" ABC123 ", original["RadioSN"]);
    }

    [Fact]
    public void DoesNotAssumeSerialNumberLengthOrPattern()
    {
        var resolved = new ConflictResolutionResult(
            [new ResolvedDatasetRow(0, "A.xlsx", 1,
                new Dictionary<string, string?> { ["RadioSN"] = "A-7/ZZ99" })], []);
        var result = MigrationValidationEngine.Validate(resolved, ["RadioSN"], ["RadioSN"]);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void RejectsUnknownRequiredColumns()
    {
        var resolved = new ConflictResolutionResult([], []);
        Assert.Throws<ArgumentException>(() =>
            MigrationValidationEngine.Validate(resolved, ["Site"], ["Missing"]));
    }
}
