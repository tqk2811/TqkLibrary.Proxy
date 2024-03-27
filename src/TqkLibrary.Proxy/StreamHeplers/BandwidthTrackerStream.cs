namespace TqkLibrary.Proxy.StreamHeplers
{
    public class BandwidthTrackerStream : AsynchronousOnlyStream
    {
        /// <summary>
        /// default: 1 second
        /// </summary>
        public TimeSpan SamplingPeriod { get; set; } = TimeSpan.FromSeconds(1);

        public virtual long TotalRead { get; private set; }
        public virtual event Action<int>? OnByteReaded;
        /// <summary>
        /// per <see cref="SamplingPeriod"/>
        /// </summary>
        public virtual event Action<double>? OnInstantReadSpeed;
        /// <summary>
        /// per <see cref="SamplingPeriod"/>
        /// </summary>
        public virtual event Action<double>? OnAverageReadSpeed;
        DateTime? _firstRead;
        DateTime? _lastSamplingRead;
        long _lastTotalSamplingRead = 0;

        public virtual long TotalWrite { get; private set; }
        public virtual event Action<int>? OnByteWrited;
        /// <summary>
        /// per <see cref="SamplingPeriod"/>
        /// </summary>
        public virtual event Action<double>? OnInstantWriteSpeed;
        /// <summary>
        /// per <see cref="SamplingPeriod"/>
        /// </summary>
        public virtual event Action<double>? OnAverageWriteSpeed;
        DateTime? _firstWrite;
        DateTime? _lastSamplingWrite;
        long _lastTotalSamplingWrite = 0;

        public BandwidthTrackerStream(Stream baseStream) : base(baseStream)
        {

        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int byte_read = await base.ReadAsync(buffer, offset, count, cancellationToken);
            TotalRead += byte_read;
            if (!_firstRead.HasValue) _firstRead = DateTime.Now;
            else OnAverageReadSpeed?.Invoke(TotalRead * SamplingPeriod.TotalSeconds / (DateTime.Now - _firstRead.Value).TotalSeconds);
            if (!_lastSamplingRead.HasValue) _lastSamplingRead = DateTime.Now;
            else
            {
                if (DateTime.Now - _lastSamplingRead.Value >= SamplingPeriod)
                {
                    double val = (TotalRead - _lastTotalSamplingRead) * SamplingPeriod.TotalSeconds / (DateTime.Now - _lastSamplingRead.Value).TotalSeconds;

                    _lastTotalSamplingRead = TotalRead;
                    _lastSamplingRead = DateTime.Now;

                    OnInstantReadSpeed?.Invoke(val);
                }
            }
            OnByteReaded?.Invoke(byte_read);
            return byte_read;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await base.WriteAsync(buffer, offset, count, cancellationToken);
            TotalWrite += count;
            if (!_firstWrite.HasValue) _firstWrite = DateTime.Now;
            else OnAverageWriteSpeed?.Invoke(TotalWrite * SamplingPeriod.TotalSeconds / (DateTime.Now - _firstWrite.Value).TotalSeconds);
            if (!_lastSamplingWrite.HasValue) _lastSamplingWrite = DateTime.Now;
            else
            {
                if (DateTime.Now - _lastSamplingWrite.Value >= SamplingPeriod)
                {
                    double val = (TotalWrite - _lastTotalSamplingWrite) * SamplingPeriod.TotalSeconds / (DateTime.Now - _lastSamplingWrite.Value).TotalSeconds;

                    _lastTotalSamplingWrite = TotalWrite;
                    _lastSamplingWrite = DateTime.Now;

                    OnInstantWriteSpeed?.Invoke(val);
                }
            }
            OnByteWrited?.Invoke(count);
        }
    }
}
