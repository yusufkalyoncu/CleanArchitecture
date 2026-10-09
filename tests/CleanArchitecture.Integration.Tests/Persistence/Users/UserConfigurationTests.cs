using CleanArchitecture.Domain.Users;
using CleanArchitecture.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CleanArchitecture.Integration.Tests.Persistence.Users;

public class UserConfigurationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task AddUser_Should_SaveToDatabase_WhenEntityIsValid()
    {
        // Arrange
        var userResult = User.Create(
            "unique2@test.com",
            "John", 
            "Doe",
            "StrongPass123!");
            
        var user = userResult.Data;

        // Act
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();

        // Assert
        var savedUser = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        
        savedUser.Should().NotBeNull();
        savedUser!.Email.Value.Should().Be("unique2@test.com");
        savedUser.Name.FirstName.Should().Be("John");
        savedUser.Name.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task AddUser_Should_ThrowDbUpdateException_WhenEmailIsNotUnique()
    {
        // Arrange
        var user1Result = User.Create(
            "duplicate2@test.com",
            "John", 
            "Doe",
            "StrongPass123!");

        var user2Result = User.Create(
            "duplicate2@test.com", // Same email
            "Jane", 
            "Smith",
            "AnotherPass123!");

        DbContext.Users.Add(user1Result.Data);
        await DbContext.SaveChangesAsync();

        // Act
        DbContext.Users.Add(user2Result.Data);
        Func<Task> act = async () => await DbContext.SaveChangesAsync();

        // Assert
        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        
        // Postgres error code for unique violation is 23505
        exception.WithInnerException<PostgresException>()
            .Where(e => e.SqlState == "23505" && e.MessageText.Contains("IX_Users_Email"));
    }
}