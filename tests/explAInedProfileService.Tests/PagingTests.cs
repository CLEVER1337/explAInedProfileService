public class PagingTests
{
    [Fact]
    public void LimitDefaultsWhenAbsent() => Assert.Equal(20, Paging.Limit(null));

    [Fact]
    public void LimitClampsToAtLeastOne() => Assert.Equal(1, Paging.Limit(0));

    [Fact]
    public void LimitClampsNegativesToOne() => Assert.Equal(1, Paging.Limit(-5));

    [Fact]
    public void LimitCapsAtOneHundred() => Assert.Equal(100, Paging.Limit(5000));

    [Fact]
    public void LimitPassesThroughValidValues() => Assert.Equal(37, Paging.Limit(37));

    [Fact]
    public void OffsetDefaultsToZero() => Assert.Equal(0, Paging.Offset(null));

    [Fact]
    public void OffsetFloorsNegatives() => Assert.Equal(0, Paging.Offset(-1));

    [Fact]
    public void OffsetHasNoUpperBound() => Assert.Equal(100_000, Paging.Offset(100_000));
}
