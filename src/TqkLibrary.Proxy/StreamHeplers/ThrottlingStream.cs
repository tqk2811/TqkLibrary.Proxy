using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.StreamHeplers
{
    public class ThrottlingStream : BandwidthTrackerStream
    {
        /// <summary>
        /// Bytes per <see cref="SamplingPeriod"/>
        /// </summary>
        public double LimitRead { get; set; }

        /// <summary>
        /// Bytes per <see cref="SamplingPeriod"/>
        /// </summary>
        public double LimitWrite { get; set; }

        public ThrottlingStream(Stream baseStream) : base(baseStream)
        {

        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int byte_read = await base.ReadAsync(buffer, offset, count, cancellationToken);
            if(LimitRead > 0)
            {

            }
            return byte_read;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await base.WriteAsync(buffer, offset, count, cancellationToken);
            if (LimitWrite > 0)
            {

            }
        }

    }
}
