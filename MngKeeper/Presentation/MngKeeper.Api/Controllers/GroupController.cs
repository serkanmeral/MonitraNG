using Microsoft.AspNetCore.Mvc;
using MediatR;
using MngKeeper.Api.Attributes;
using MngKeeper.Application.Features.Group.Commands.CreateGroup;
using MngKeeper.Application.Features.Group.Commands.UpdateGroup;
using MngKeeper.Application.Features.Group.Commands.DeleteGroup;
using MngKeeper.Application.Features.Group.Queries.GetGroups;
using MngKeeper.Application.Features.Group.Queries.GetGroup;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [AdminAuthorization] // Temporarily disabled for testing
    public class GroupController : ControllerBase
    {
        private readonly IMediator _mediator;

        public GroupController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<ActionResult<CreateGroupResponse>> CreateGroup([FromBody] CreateGroupCommand command)
        {
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            // Get domain from token claims
            var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
            return CreatedAtAction(nameof(GetGroups), new { groupId = response.GroupId }, response);
        }

        [HttpGet]
        public async Task<ActionResult<GetGroupsResponse>> GetGroups(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool? isActive = null)
        {
            var query = new GetGroupsQuery 
            { 
                Page = page, 
                PageSize = pageSize,
                SearchTerm = searchTerm,
                IsActive = isActive
            };
            var response = await _mediator.Send(query);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        // Temporarily disabled due to schema conflicts
        // [HttpGet("{groupId}")]
        // public async Task<ActionResult<GetGroupResponse>> GetGroup(string groupId)
        // {
        //     var query = new GetGroupQuery { GroupId = groupId };
        //     var response = await _mediator.Send(query);
        //     
        //     if (!response.IsSuccess)
        //         return NotFound(response);
        // 
        //     return Ok(response);
        // }

        [HttpPut("{groupId}")]
        public async Task<ActionResult<UpdateGroupResponse>> UpdateGroup(string groupId, [FromBody] UpdateGroupCommand command)
        {
            command.GroupId = groupId;
            
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpDelete("{groupId}")]
        public async Task<ActionResult<DeleteGroupResponse>> DeleteGroup(string groupId)
        {
            var command = new DeleteGroupCommand { GroupId = groupId };
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return NoContent();
        }

        [HttpGet("health")]
        public ActionResult<string> Health()
        {
            return Ok("Group Controller is working!");
        }
    }
}
