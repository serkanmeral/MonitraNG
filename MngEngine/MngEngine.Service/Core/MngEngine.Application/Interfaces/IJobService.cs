using MngEngine.Domain.Entities.Job;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Interfaces
{
    public interface IJobService
    {
        Task<IEnumerable<JobDetail>> GetJobs();

        Task AddJob(JobSchedule jobSchedule);

        Task DeleteJob(string jobName);

        /// <summary>Quartz job'ı anında tetikler (örn. ConfigSyncJob).</summary>
        Task<bool> TriggerJobAsync(string jobName);
    }
}