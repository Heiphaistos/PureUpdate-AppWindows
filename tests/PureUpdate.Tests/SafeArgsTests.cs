using PureUpdate.Core.Services;
using Xunit;

namespace PureUpdate.Tests;

public class SafeArgsTests
{
    [Theory]
    [InlineData("Microsoft.PowerToys")]
    [InlineData("Git.Git")]
    [InlineData("9NBLGGH4NNS1")]
    [InlineData("Vendor.Long…")]
    public void AcceptsNormalIds(string id) => Assert.Equal(id, SafeArgs.PackageId(id));

    [Theory]
    [InlineData("a\" --source evil")]
    [InlineData("a'b")]
    [InlineData("a&calc")]
    [InlineData("a b")]
    [InlineData("a`b")]
    [InlineData("")]
    public void RejectsInjection(string id) => Assert.Throws<ArgumentException>(() => SafeArgs.PackageId(id));

    [Fact]
    public void ProgramFilesOnly()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.True(SafeArgs.IsUnderProgramFiles(Path.Combine(pf, "PureUpdate", "PureUpdate.exe")));
        Assert.False(SafeArgs.IsUnderProgramFiles(@"C:\Users\x\Downloads\PureUpdate.exe"));
        Assert.False(SafeArgs.IsUnderProgramFiles(pf + @"\..\Users\x\a.exe"));
        Assert.False(SafeArgs.IsUnderProgramFiles(pf + "Evil" + Path.DirectorySeparatorChar + "a.exe"));
        Assert.False(SafeArgs.IsUnderProgramFiles(""));
    }
}
