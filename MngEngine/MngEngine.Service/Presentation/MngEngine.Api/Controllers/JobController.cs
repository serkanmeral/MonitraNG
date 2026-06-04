using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MngEngine.Application.Interfaces;
using MngEngine.Domain.Entities.Job;

namespace MngEngine.Api.Controllers
{
    [ApiController]
    [Route("api/jobs")]
    public class JobController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpGet]
        public async Task<IEnumerable<JobDetail>> GetJobs()
        {
            return await _jobService.GetJobs();
        }

        [HttpPost]
        public async Task<IActionResult> AddJob([FromBody] JobSchedule jobSchedule)
        {
            await _jobService.AddJob(jobSchedule);
            return Ok();
        }

        [HttpDelete("{jobName}")]
        public async Task<IActionResult> DeleteJob(string jobName)
        {
            await _jobService.DeleteJob(jobName);
            return Ok();
        }
    }
}