
using Stamon.Types.Interfaces;
using Stamon.Types.Model;
using System.Collections.Concurrent;

namespace Stamon.Services
{

    public class StamonSlot
    {
        private readonly IStamonPersistance _store;

        private readonly ConcurrentDictionary<string, Types.Model.Monitor> _monitors = new();
        private readonly Scale _scale;
        private readonly long _duration;

        public StamonSlot(Scale scale, long duration, IStamonPersistance store, bool isDefault = false)
        {
            _scale = scale;
            _duration = duration;
            Default = isDefault;
            _store = store;
        }

        public List<string> GetMonitorNames()
        {
            return _monitors.Values.Select(x => x.Name).Distinct().ToList();
        }

        public async Task StatisticEvent(string monitorName)
        {

            if (_monitors.TryGetValue(monitorName, out var monitor))
            {
                await _store.AddMonitor(monitor);
            }
            else
            {
                var m = new Types.Model.Monitor() { Name = monitorName, Duration = _duration, Scale = _scale };
                _monitors.TryAdd(monitorName, m);
                await _store.AddMonitor(m);
            }

        }

        public async Task<ICollection<ActionPerTime>> Get(string monitorName)
        {
            if (_monitors.TryGetValue(monitorName, out var slot))
            {
                return await _store.GetMonitor(slot);
            }

            return new List<ActionPerTime>();
        }

        public bool Default { get; set; }

    }
}
