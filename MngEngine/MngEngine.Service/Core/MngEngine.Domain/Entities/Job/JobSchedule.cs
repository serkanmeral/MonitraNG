using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Domain.Entities.Job
{
    public class JobDetail
    {
        public string Name { get; set; } = "";
        public string Group { get; set; } = "";
        public string Description { get; set; } = "";
        /// <summary>Cron ifadesi (Quartz format).</summary>
        public string? CronExpression { get; set; }
        /// <summary>Bir sonraki çalışma zamanı (UTC).</summary>
        public DateTimeOffset? NextFireTimeUtc { get; set; }
        /// <summary>CollectorJob için: hangi asset'lerin hangi periyotla toplanacağı. Her period grubu için ayrı trigger çalışır.</summary>
        public List<JobAssetSchedule>? Assets { get; set; }
    }

    /// <summary>CollectorJob detayında asset-periyot bilgisi.</summary>
    public class JobAssetSchedule
    {
        public string AssetId { get; set; } = "";
        public string AssetName { get; set; } = "";
        public string AgentName { get; set; } = "";
        /// <summary>Config'teki period cron (örn. */5 * * * *). Engine bu period'a sahip trigger'da bu asset'i toplar.</summary>
        public string? PeriodExpression { get; set; }
    }

    public class JobSchedule
    {
        public JobSchedule(Type jobType, string cronExpression)
        {
            JobType = jobType;
            CronExpression = cronExpression;
        }

        public Type JobType { get; }
        public string CronExpression { get; }
    }
}