using HotChocolate;
using MngKeeper.Application.Common.DTOs;

namespace MngKeeper.Api.GraphQL
{
    public class Query
    {
        public List<DomainDto> GetDomains()
        {
            return new List<DomainDto>
            {
                new DomainDto
                {
                    Id = "1",
                    Name = "Test Domain",
                    Description = "Test domain for GraphQL",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
        }

        public List<UserDto> GetUsers()
        {
            return new List<UserDto>
            {
                new UserDto
                {
                    UserId = "1",
                    Username = "testuser",
                    Email = "test@example.com",
                    FirstName = "Test",
                    LastName = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = "system",
                    Groups = new List<string> { "admins" },
                    Permissions = new List<string> { "read", "write" }
                }
            };
        }

        public List<GroupDto> GetGroups()
        {
            return new List<GroupDto>
            {
                new GroupDto
                {
                    GroupId = "1",
                    Name = "Test Group",
                    Description = "Test group for GraphQL",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = "system",
                    Permissions = new List<string> { "read", "write" }
                }
            };
        }
    }

    public class DomainDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // UserDto is now imported from MngKeeper.Application.Common.DTOs

    // GroupDto is now imported from MngKeeper.Application.Common.DTOs
}
