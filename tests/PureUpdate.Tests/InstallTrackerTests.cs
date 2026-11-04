using PureUpdate.Core.Services;
using Xunit;

namespace PureUpdate.Tests;

public class InstallTrackerTests
{
    [Fact]
    public void Counts_nested_and_parallel_operations_and_signals_idle_once()
    {
        int idle = 0;
        void OnIdle() => Interlocked.Increment(ref idle);
        InstallTracker.Idle += OnIdle;
        try
        {
            Assert.False(InstallTracker.IsBusy);
            var a = InstallTracker.Begin("A");
            var b = InstallTracker.Begin("B");
            Assert.Equal("A et B", InstallTracker.CurrentName);
            a.Dispose();
            a.Dispose(); // double Dispose sans effet
            Assert.Equal("B", InstallTracker.CurrentName);
            Assert.Equal(0, idle);
            b.Dispose();
            Assert.Null(InstallTracker.CurrentName);
            Assert.Equal(1, idle);
        }
        finally { InstallTracker.Idle -= OnIdle; }
    }

    [Fact]
    public void Releases_on_exception_and_across_threads()
    {
        static void Fail()
        {
            using var op = InstallTracker.Begin("X");
            throw new InvalidOperationException();
        }
        Assert.Throws<InvalidOperationException>(Fail);
        Parallel.For(0, 200, _ => { using var op = InstallTracker.Begin("P"); });
        Assert.False(InstallTracker.IsBusy);
    }
}
