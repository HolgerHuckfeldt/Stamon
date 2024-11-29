using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace Stamon.Services
{

    public class StamonOptions : IOptions<StamonOptions>
    {
        /// <summary>
        /// The configuration used to connect to Redis.
        /// </summary>
        public string Configuration { get; set; }

        /// <summary>
        /// Use the Grpc-Endpoint
        /// </summary>

        public bool UseFallback { get; set; } = true;

        public string Username { get; set; }

        public string Password { get; set; }

        StamonOptions IOptions<StamonOptions>.Value
        {
            get { return this; }
        }
    }
}
