namespace Weekmark.App.Tests;

public sealed class SingleInstanceTests
{
    static string NewName() => $"Weekmark.Test.{Guid.NewGuid():N}";

    static SingleInstance? AcquireOnOtherThread(string name, bool dispose)
    {
        SingleInstance? result = null;
        var t = new Thread(() =>
        {
            result = SingleInstance.TryAcquire(name);
            if (dispose) result?.Dispose();
        });
        t.Start();
        t.Join();
        return result;
    }

    [Fact]
    public void SecondInstanceIsRefused()
    {
        var name = NewName();
        using (var first = SingleInstance.TryAcquire(name))
        {
            Assert.NotNull(first);
            Assert.Null(AcquireOnOtherThread(name, dispose: true));
        }
        // Disposal releases it: the next launch gets it.
        Assert.NotNull(AcquireOnOtherThread(name, dispose: true));
    }

    [Fact]
    public void SecondLaunchSignalsFirst()
    {
        var name = NewName();
        using var first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);
        using var got = new ManualResetEventSlim();
        first.ActivationRequested += got.Set;

        Assert.True(SingleInstance.Signal(name));
        Assert.True(got.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void SignalWithoutFirstInstanceFails() => Assert.False(SingleInstance.Signal(NewName()));
}
