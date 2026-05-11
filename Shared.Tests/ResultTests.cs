using System;
using Shared;
using Xunit;

namespace Shared.Tests;

public class ResultTests
{
    [Fact]
    public void OkAndErrBranchesWork()
    {
        var ok = Result<int, string>.Ok(42);
        var err = Result<int, string>.Err("bad");

        Assert.True(ok.IsOk);
        Assert.False(ok.IsError);
        Assert.Equal(42, ok.Value);

        Assert.True(err.IsError);
        Assert.False(err.IsOk);
        Assert.Equal("bad", err.Error);
    }

    [Fact]
    public void WrongBranchAccessThrowsInvalidOperation()
    {
        var ok = Result<int, string>.Ok(1);
        var err = Result<int, string>.Err("oops");

        Assert.Throws<InvalidOperationException>(() => _ = ok.Error);
        Assert.Throws<InvalidOperationException>(() => _ = err.Value);
    }

    [Fact]
    public void MatchMapMapErrorBindWork()
    {
        var ok = Result<int, string>.Ok(5);
        var err = Result<int, string>.Err("e");

        Assert.Equal("5", ok.Match(v => v.ToString(), e => e));
        Assert.Equal("e", err.Match(v => v.ToString(), e => e));

        Assert.Equal(10, ok.Map(v => v * 2).Value);
        Assert.Equal("e!", err.MapError(e => e + "!").Error);

        var bound = ok.Bind(v => Result<int, string>.Ok(v + 1));
        Assert.Equal(6, bound.Value);
    }

    [Fact]
    public void TapTapErrorTryGetValueAndValueOrWork()
    {
        int tapped = 0;
        string tappedErr = string.Empty;

        var ok = Result<int, string>.Ok(7)
            .Tap(v => tapped = v)
            .TapError(e => tappedErr = e);

        Assert.Equal(7, tapped);
        Assert.Equal(string.Empty, tappedErr);
        Assert.True(ok.TryGetValue(out var okValue, out var okError));
        Assert.Equal(7, okValue);
        Assert.Null(okError);

        var err = Result<int, string>.Err("fail")
            .Tap(v => tapped = v)
            .TapError(e => tappedErr = e);

        Assert.Equal("fail", tappedErr);
        Assert.False(err.TryGetValue(out _, out var errValue));
        Assert.Equal("fail", errValue);
        Assert.Equal(99, err.ValueOr(99));
        Assert.Equal(4, err.ValueOr(e => e.Length));
    }

    [Fact]
    public void EnsureAndToStringWork()
    {
        var ok = Result<int, string>.Ok(2);
        var ensuredOk = ok.Ensure(v => v % 2 == 0, () => "not-even");
        var ensuredErr = ok.Ensure(v => v % 2 == 1, () => "not-odd");

        Assert.True(ensuredOk.IsOk);
        Assert.True(ensuredErr.IsError);
        Assert.Equal("not-odd", ensuredErr.Error);
        Assert.Equal("Ok(2)", ok.ToString());
        Assert.Equal("Err(not-odd)", ensuredErr.ToString());
    }

    [Fact]
    public void UnitResultShapeWorks()
    {
        var ok = Result<string>.Ok();
        var err = Result<string>.Err("bad");

        Assert.True(ok.IsOk);
        Assert.False(ok.IsError);

        Assert.True(err.IsError);
        Assert.Equal("bad", err.Error);
        Assert.Equal("Err(bad)", err.ToString());
    }
}
