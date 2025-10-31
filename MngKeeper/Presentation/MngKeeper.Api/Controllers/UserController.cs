using Microsoft.AspNetCore.Mvc;
using MediatR;
using MngKeeper.Application.Features.User.Commands.CreateUser;
using MngKeeper.Application.Features.User.Commands.UpdateUser;
using MngKeeper.Application.Features.User.Commands.DeleteUser;
using MngKeeper.Application.Features.User.Commands.AddUserToGroup;
using MngKeeper.Application.Features.User.Commands.RemoveUserFromGroup;
using MngKeeper.Application.Features.User.Queries.GetUser;
using MngKeeper.Application.Features.User.Queries.GetUsers;
using MngKeeper.Api.Attributes;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [AdminAuthorization] // Temporarily disabled for testing
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<ActionResult<CreateUserResponse>> CreateUser([FromBody] CreateUserCommand command)
        {
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            // Get domain from token claims
            var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
            return CreatedAtAction(nameof(GetUser), new { userId = response.UserId }, response);
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<GetUserResponse>> GetUser(string userId)
        {
            var query = new GetUserQuery { UserId = userId };
            var response = await _mediator.Send(query);
            
            if (!response.IsSuccess)
                return NotFound(response);

            return Ok(response);
        }

        [HttpGet]
        public async Task<ActionResult<GetUsersResponse>> GetUsers(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool? isActive = null)
        {
            var query = new GetUsersQuery 
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

        [HttpPut("{userId}")]
        public async Task<ActionResult<UpdateUserResponse>> UpdateUser(string userId, [FromBody] UpdateUserCommand command)
        {
            command.UserId = userId;
            
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpDelete("{userId}")]
        public async Task<ActionResult<DeleteUserResponse>> DeleteUser(string userId)
        {
            var command = new DeleteUserCommand { UserId = userId };
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return NoContent();
        }

        [HttpPost("{userId}/groups/{groupId}")]
        public async Task<ActionResult<AddUserToGroupResponse>> AddUserToGroup(string userId, string groupId)
        {
            var command = new AddUserToGroupCommand { UserId = userId, GroupId = groupId };
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpDelete("{userId}/groups/{groupId}")]
        public async Task<ActionResult<RemoveUserFromGroupResponse>> RemoveUserFromGroup(string userId, string groupId)
        {
            var command = new RemoveUserFromGroupCommand { UserId = userId, GroupId = groupId };
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return NoContent();
        }
    }
}
