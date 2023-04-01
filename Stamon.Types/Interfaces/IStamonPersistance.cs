using Stamon.Types.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stamon.Types.Interfaces
{
    public interface IStamonPersistance
    {
        Task AddMonitor(Monitor monitor);
        Task<ICollection<ActionPerTime>> GetMonitor(Monitor monitor);

        Task<ICollection<ActionPerTime>> GetMonitor(Monitor monitor, DateTime from, DateTime to);

        Task<ICollection<string>> GetMonitorNames();
    }

    public interface IStamonMemoryPersistance : IStamonPersistance
    {

    }

    public interface IStamonDatabasePersistance : IStamonPersistance
    {

    }
}
