using System.Diagnostics;

static class PerformanceMonitor
{
    public static double RayLoopTime { get; set; }
    public static double SpriteDrawTime { get; set; }
    public static double zBufferDrawTime { get; set; }
    public static double depthTextureTime { get; set; }

    private static readonly Stopwatch timer = new();

    public static IDisposable Measure(Action<double> setter)
    {
        timer.Restart();
        return new DisposableAction(() => {
            timer.Stop();
            setter(timer.Elapsed.TotalMilliseconds);
        });
    }

    private struct DisposableAction : IDisposable
    {
        private readonly Action action;
        public DisposableAction(Action a) => action = a;
        public void Dispose() => action?.Invoke();
    }
}