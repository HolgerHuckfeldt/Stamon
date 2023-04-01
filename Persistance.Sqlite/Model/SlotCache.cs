using System;
using System.Collections.Generic;
using System.Text;

namespace Persistance.Sqlite.Model
{
    internal class SlotCache
    {
        public long? SlotId { get; set; }

        public string Name { get; set; }

        public Stamon.Types.Model.Scale Scale { get; set; }

        public string Key => Name + Scale;
    }
}
