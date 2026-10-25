using System.IO.Pipes;

namespace Weekmark.App;

/// <summary>
/// One Weekmark per user session: a named mutex decides who runs; the winner listens on a named pipe of the same
/// name, and a second launch connects to it (<see cref="Signal"/>) to ask for the widget, then exits.
/// Both work on Windows and macOS (.NET emulates named mutexes and maps pipes to Unix sockets there).
/// </summary>
public sealed class SingleInstance : IDisposable
{
    readonly Mutex mutex;
    readonly string pipeName;
    readonly CancellationTokenSource stop = new();

    /// <summary>Raised on a background thread when another launch asks this one to show itself.</summary>
    public event Action? ActivationRequested;

    SingleInstance(Mutex mutex, string name)
    {
        this.mutex = mutex;
        pipeName = PipeName(name);
        // The first server exists before TryAcquire returns, so a launch right after this one finds it listening.
        NamedPipeServerStream? first = null;
        try { first = NewServer(); } catch (Exception) { } // Listen retries
        _ = Task.Run(() => Listen(first));
    }

    NamedPipeServerStream NewServer() => new(pipeName, PipeDirection.In, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    // macOS limits socket paths (where .NET puts pipes) to 104 characters: hash long names down.
    static string PipeName(string name) =>
        "wm-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name)))[..16];

    /// <summary>Null when another instance (any thread or process in this session) already holds <paramref name="name"/>.</summary>
    public static SingleInstance? TryAcquire(string name)
    {
        var m = new Mutex(false, $@"Local\{name}");
        try
        {
            if (m.WaitOne(0)) return new SingleInstance(m, name);
        }
        catch (AbandonedMutexException) { return new SingleInstance(m, name); } // previous owner crashed: it's ours now
        m.Dispose();
        return null;
    }

    /// <summary>Asks the running instance to bring its widget forward. False when nobody is listening.</summary>
    public static bool Signal(string name)
    {
        try
        {
            using var c = new NamedPipeClientStream(".", PipeName(name), PipeDirection.Out, PipeOptions.CurrentUserOnly);
            c.Connect(1000);
            c.WriteByte(1);
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException) { return false; }
    }

    async Task Listen(NamedPipeServerStream? next)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var server = next ?? NewServer();
                next = null;
                await server.WaitForConnectionAsync(stop.Token);
                var buf = new byte[1];
                _ = await server.ReadAsync(buf, stop.Token);
                ActivationRequested?.Invoke();
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) { await Task.Delay(200); } // anything else: keep listening, so activation never stops
        }
        next?.Dispose(); // stopped before the first server was used
    }

    public void Dispose()
    {
        stop.Cancel();
        try { mutex.ReleaseMutex(); }
        catch (ApplicationException) { } // disposed from a thread that doesn't own it; process exit releases it
        mutex.Dispose();
    }
}
