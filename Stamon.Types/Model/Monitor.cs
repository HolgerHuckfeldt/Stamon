using System;
using System.Collections.Generic;
using System.Text;

namespace Stamon.Types.Model
{
    public class Monitor
    {
        /// <summary>
        /// The name of the monitor
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The Scale of the monitor, like second or minute
        /// </summary>
        public Scale Scale { get; set; }

        /// <summary>
        /// Duration, values are in 'Scale'-scale.
        /// </summary>
        public long Duration { get; set; }
    }
}
