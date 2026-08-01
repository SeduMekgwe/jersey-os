using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class ImportTests
{
    [Fact]
    public void ApproveIsIdempotentAfterApply()
    {
        var batch = new ImportBatch(Guid.NewGuid(), Guid.NewGuid(), "feed.csv", "imports/a.csv", "text/csv", "corr");
        batch.MarkParsing();
        var item = batch.AddItem("{}", "Home", "home", "HOME-M", "M", "H1", "Arsenal", "2025/26", 5, null, ImportMatchHint.New, null, null);
        batch.MarkReadyForReview();
        item.Approve();
        item.MarkApplied(Guid.NewGuid(), Guid.NewGuid());
        item.Approve();
        Assert.Equal(ImportItemStatus.Applied, item.Status);
    }

    [Fact]
    public void AmbiguousMatchCannotApprove()
    {
        var batch = new ImportBatch(Guid.NewGuid(), Guid.NewGuid(), "feed.csv", "imports/a.csv", "text/csv", "corr");
        batch.MarkParsing();
        var item = batch.AddItem("{}", "Home", "home", "HOME-M", "M", null, null, null, null, null, ImportMatchHint.Ambiguous, null, null);
        batch.MarkReadyForReview();
        Assert.Throws<InvalidOperationException>(() => item.Approve());
    }
}
