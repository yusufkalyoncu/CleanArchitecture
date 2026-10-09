using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Users.GetUsers;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Integration.Tests.Infrastructure;
using CleanArchitecture.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Integration.Tests.Application.Users;

public class GetUserQueryTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetUserQuery_Should_ReturnUser_WhenUserExists()
    {
        // Arrange
        var userResult = User.Create(
            "getuser@test.com",
            "Alice",
            "Wonderland",
            "StrongPass123!");
            
        var user = userResult.Data;
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();

        var query = new GetUserQuery(user.Id);
        
        // Resolve the internal handler using DI
        var handler = ServiceProvider.GetRequiredService<IQueryHandler<GetUserQuery, GetUserQueryResponse>>();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Email.Should().Be("getuser@test.com");
        result.Data.Name.Should().Be("Alice Wonderland");
    }
    
    [Fact]
    public async Task GetUserQuery_Should_ReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        var query = new GetUserQuery(Guid.NewGuid());
        var handler = ServiceProvider.GetRequiredService<IQueryHandler<GetUserQuery, GetUserQueryResponse>>();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
    }
    
    [Fact]
    public async Task GetUsersQuery_Should_ReturnAllUsers()
    {
        // Arrange
        var user1Result = User.Create("listuser1@test.com", "User", "One", "Pass123!");
        var user2Result = User.Create("listuser2@test.com", "User", "Two", "Pass123!");
        
        DbContext.Users.AddRange(user1Result.Data, user2Result.Data);
        await DbContext.SaveChangesAsync();

        var query = new GetUsersQuery();
        var handler = ServiceProvider.GetRequiredService<IQueryHandler<GetUsersQuery, IEnumerable<GetUsersQueryResponse>>>();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var list = result.Data.ToList();
        list.Should().Contain(u => u.Email == "listuser1@test.com");
        list.Should().Contain(u => u.Email == "listuser2@test.com");
    }
}