using System;
using System.Threading.Tasks;
using Humanizer;

namespace SIBR.Storage.Ingest
{
    public abstract class IntervalWorker : BaseWorker
    {
        protected TimeSpan Interval { get; set; }
        private readonly TimeSpan _offset;

        protected IntervalWorker(IServiceProvider services, IntervalWorkerConfiguration config) : base(services)
        {
            Interval = config.Interval;
            _offset = config.Offset;
        }

        protected abstract Task RunInterval();

        protected virtual Task BeforeStart() => Task.CompletedTask;

        protected override async Task Run()
        {
            await BeforeStart();
            while (true)
            {
                async Task Inner()
                {
                    try
                    {
                        _logger.Debug("Running interval worker {WorkerType} (interval of {Interval})", GetType().Name,
                            Interval.Humanize());
                        await RunInterval();
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Error running worker {WorkerType}", GetType().Name);
                    }
                }

                var _ = Inner();

                // in case times are a bit off and we end up still within the same window
                await Task.Delay(TimeSpan.FromMilliseconds(10));

                // (arbitrary, just for tick alignment mostly)
                var epoch = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    .Add(_offset);

                var target = DateTimeOffset.UtcNow
                             // subtract amount of time "into" this interval
                             - TimeSpan.FromTicks((DateTimeOffset.UtcNow - epoch).Ticks % Interval.Ticks)
                             // get the next interval
                             + Interval;
                
                // if we're really close to the target, assume a timing issue and wait another interval
                var waitTime = target - DateTimeOffset.UtcNow;
                if (waitTime < TimeSpan.FromMilliseconds(100))
                {
                    _logger.Warning("Wait time for {WorkerType} is too small ({WaitTime}), adding another interval",
                        GetType().Name, waitTime);
                    target += Interval;
                }

                // May need multiple loops in case of delay inaccuracies
                while ((waitTime = target - DateTimeOffset.UtcNow) > TimeSpan.Zero)
                    await Task.Delay(waitTime);
            }
        }
    }
}