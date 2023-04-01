using System;
using System.Collections.Generic;
using System.Text;

namespace Persistance.Sqlite.Model
{
    internal class ItemCache
    {
        public long SlotId { get; set; }

        public long ItemId { get; set; }

        public DateTime LastInsert { get; set; }

        public long Count { get; set; }
    }
}
