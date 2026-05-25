using System.Threading;

namespace CS2GameHelper.Utils;

public abstract class ThreadedServiceBase : IDisposable
{
    private Thread? _thread;
    private volatile bool _isRunning;
    private bool _disposed;

    protected virtual string ThreadName => nameof(ThreadedServiceBase);

    protected virtual TimeSpan ThreadTimeout { get; set; } = TimeSpan.FromSeconds(3);

    protected virtual TimeSpan ThreadFrameSleep { get; set; } = TimeSpan.FromMilliseconds(1);

    protected ThreadedServiceBase()
    {
    }

    public void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _thread = new Thread(ThreadStart)
        {
            Name = ThreadName,
            IsBackground = true
        };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;

        if (_thread != null && _thread.IsAlive)
        {
            try
            {
                _thread.Interrupt();
                if (!_thread.Join(ThreadTimeout))
                {
                    // If it didn't stop gracefully, we just move on.
                    // In a more aggressive implementation we might Abort() but it's deprecated.
                }
            }
            catch (ThreadStateException) { }
            catch (ThreadInterruptedException) { }
        }

        _thread = null;
    }

    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            Stop();
        }

        _disposed = true;
    }

    private void ThreadStart()
    {
        try
        {
            while (_isRunning)
            {
                FrameAction();
                Thread.Sleep(ThreadFrameSleep);
            }
        }
        catch (ThreadInterruptedException)
        {
            // expected during shutdown
        }
        catch (NullReferenceException)
        {
            // legacy behaviour retained for existing services
        }
        finally
        {
            // Only clear _isRunning if the current thread is the one that was supposed to be running.
            // This prevents a race condition where a newly started thread's flag is cleared by an exiting old thread.
            if (ReferenceEquals(Thread.CurrentThread, _thread))
            {
                _isRunning = false;
            }
        }
    }

    protected abstract void FrameAction();
}
