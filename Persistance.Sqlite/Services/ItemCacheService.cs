using Persistance.Sqlite.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Persistance.Sqlite.Services
{
    internal class ItemCacheService
    {
        private readonly ConcurrentDictionary<long, ItemCache> _itemCache = new ConcurrentDictionary<long, ItemCache>();

        /// <summary>
        /// Get the last inserted Date of an item.
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
        public ItemCache? GetLastItemForScale(long scale)
        {
            if(_itemCache.TryGetValue(scale, out var cachedItem))
            {
                return cachedItem;
            }

            return null;
        }

        /// <summary>
        /// Update item in Cache.
        /// </summary>
        /// <param name="item"></param>
        public void AddScaleItem(ItemCache item)
        {
            if (_itemCache.TryGetValue(item.SlotId, out var oldItem))
            {
                _itemCache.TryUpdate(item.SlotId, item, oldItem);
            }
            else
            {
                _itemCache.TryAdd(item.SlotId, item);
            }
        }

    }
}
