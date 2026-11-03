using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class DuplicateGroupClassifierTests
{
    [Theory]
    [InlineData("", "", DuplicateGroupKind.Identical)]
    [InlineData("", "CL12345", DuplicateGroupKind.Complementary)]
    [InlineData("CL111", "CL222", DuplicateGroupKind.Conflicting)]
    public void ClassifiesGroups(string first, string second, DuplicateGroupKind expected)
    {
        var merged = new DatasetMergeEngine().Merge([
            new MergeDataset("A", [new Dictionary<string, string?> { ["Site"] = "102", ["Lock"] = first }]),
            new MergeDataset("B", [new Dictionary<string, string?> { ["Site"] = "102", ["Lock"] = second }])
        ]);
        var detected = new DuplicateDetectionEngine().Detect(merged, ["Site"]);
        var result = DuplicateGroupClassifier.Classify(merged, Assert.Single(detected.Groups));
        Assert.Equal(expected, result.Kind);
        Assert.Equal(expected == DuplicateGroupKind.Complementary, result.MissingInformationColumns.Contains("Lock"));
        Assert.Equal(expected == DuplicateGroupKind.Conflicting, result.DisagreementColumns.Contains("Lock"));
    }

    [Fact]
    public void GenuineDisagreementTakesPriorityOverMissingInformation()
    {
        var merged = new DatasetMergeEngine().Merge([
            new MergeDataset("A", [new Dictionary<string, string?> { ["Site"] = "102", ["Lock"] = "", ["Radio"] = "A" }]),
            new MergeDataset("B", [new Dictionary<string, string?> { ["Site"] = "102", ["Lock"] = "CL123", ["Radio"] = "B" }])
        ]);
        var detected = new DuplicateDetectionEngine().Detect(merged, ["Site"]);
        var result = DuplicateGroupClassifier.Classify(merged, Assert.Single(detected.Groups));
        Assert.Equal(DuplicateGroupKind.Conflicting, result.Kind);
        Assert.Contains("Lock", result.MissingInformationColumns);
        Assert.Contains("Radio", result.DisagreementColumns);
    }
}
