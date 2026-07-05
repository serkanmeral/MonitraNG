using Microsoft.AspNetCore.Mvc;
using MediatR;
using MngKeeper.Api.Attributes;
using MngKeeper.Application.Features.Group.Commands.CreateGroup;
using MngKeeper.Application.Features.Group.Commands.UpdateGroup;
using MngKeeper.Application.Features.Group.Commands.UpdateGroupApplicationScope;
using MngKeeper.Application.Features.Group.Commands.DeleteGroup;
using MngKeeper.Application.Features.Group.Queries.GetGroups;
using MngKeeper.Application.Features.Group.Queries.GetGroup;
using MngKeeper.Application.Features.Group.Queries.GetGroupsByIds;
using MngKeeper.Domain.Enums;
using MngKeeper.Application.Features.Group.Queries.ExportGroups;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ManagerAuthorization] // Allows both Admin and Manager users
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
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? includeInApplication = null,
            [FromQuery] int? provisioningSource = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            UserProvisioningSource? sourceFilter = null;
            if (provisioningSource.HasValue && Enum.IsDefined(typeof(UserProvisioningSource), provisioningSource.Value))
            {
                sourceFilter = (UserProvisioningSource)provisioningSource.Value;
            }

            var query = new GetGroupsQuery 
            { 
                Page = page, 
                PageSize = pageSize,
                SearchTerm = searchTerm,
                IsActive = isActive,
                IncludeInApplication = includeInApplication,
                ProvisioningSource = sourceFilter,
                SortBy = sortBy,
                SortOrder = sortOrder,
            };
            var response = await _mediator.Send(query);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpGet("{groupId}")]
        public async Task<ActionResult<GetGroupResponse>> GetGroup(string groupId)
        {
            var query = new GetGroupQuery { GroupId = groupId };
            var response = await _mediator.Send(query);
            
            if (!response.IsSuccess)
                return NotFound(response);
        
            return Ok(response);
        }

        /// <summary>Toplu grup çözümü (MO dizin/by-ids): N+1 yerine tek istek; id + ad + aktif döner.</summary>
        [HttpPost("by-ids")]
        public async Task<ActionResult<GetGroupsByIdsResponse>> GetGroupsByIds([FromBody] GetGroupsByIdsQuery query)
        {
            var response = await _mediator.Send(query);

            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpPut("{groupId}")]
        public async Task<ActionResult<UpdateGroupResponse>> UpdateGroup(string groupId, [FromBody] UpdateGroupCommand command)
        {
            command.GroupId = groupId;
            
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>Kurumsal ve yerel gruplar — yalnızca MonitraNG uygulama kapsamı flag'i.</summary>
        [HttpPatch("{groupId}/application-scope")]
        public async Task<ActionResult<UpdateGroupApplicationScopeResponse>> UpdateGroupApplicationScope(
            string groupId,
            [FromBody] UpdateGroupApplicationScopeCommand command)
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
            // Get domain from token claims
            var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
            
            if (claims?.DomainId == null)
            {
                return BadRequest(new DeleteGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Domain information not found in token."
                });
            }
            
            var command = new DeleteGroupCommand 
            { 
                GroupId = groupId,
                DomainId = claims.DomainId
            };
            
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return NoContent();
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportGroups(
            [FromQuery] string format = "csv",
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool? isActive = null)
        {
            var query = new ExportGroupsQuery
            {
                Format = format,
                SearchTerm = searchTerm,
                IsActive = isActive
            };
            
            var response = await _mediator.Send(query);
            
            if (!response.IsSuccess)
            {
                return BadRequest(new { message = response.ErrorMessage });
            }

            return File(response.FileContent, response.ContentType, response.FileName);
        }

        [HttpGet("health")]
        public ActionResult<string> Health()
        {
            return Ok("Group Controller is working!");
        }
    }
}
