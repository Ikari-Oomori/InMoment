using FluentAssertions;
using InMoment.Domain.Users;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class UserRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistUser()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var user = CreateUser("user@test.com", "user1", "Alice", "Smith");

        await repo.AddAsync(user, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.Users.FirstOrDefaultAsync(x => x.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.Email.Should().Be("user@test.com");
        saved.UserName.Should().Be("user1");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnUser_WhenExists()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("user@test.com", "user1", "Alice", "Smith");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.GetByIdAsync(user.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnUser_WhenExists()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("user@test.com", "user1", "Alice", "Smith");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.GetByEmailAsync("user@test.com", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnNull_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.GetByEmailAsync("missing@test.com", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserNameAsync_ShouldReturnUser_WhenExists()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("user@test.com", "user1", "Alice", "Smith");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.GetByUserNameAsync("user1", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByUserNameAsync_ShouldReturnNull_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.GetByUserNameAsync("missing", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EmailExistsAsync_ShouldReturnTrue_WhenExists()
    {
        await using var db = CreateDbContext();
        db.Users.Add(CreateUser("user@test.com", "user1", "Alice", "Smith"));
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.EmailExistsAsync("user@test.com", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EmailExistsAsync_ShouldReturnFalse_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.EmailExistsAsync("missing@test.com", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserNameExistsAsync_ShouldReturnTrue_WhenExists()
    {
        await using var db = CreateDbContext();
        db.Users.Add(CreateUser("user@test.com", "user1", "Alice", "Smith"));
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.UserNameExistsAsync("user1", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserNameExistsAsync_ShouldReturnFalse_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.UserNameExistsAsync("missing", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldReturnMatchingUsers()
    {
        await using var db = CreateDbContext();

        var user1 = CreateUser("u1@test.com", "user1", "Alice", "Smith");
        var user2 = CreateUser("u2@test.com", "user2", "Bob", "Smith");
        var user3 = CreateUser("u3@test.com", "user3", "Carol", "Smith");

        db.Users.AddRange(user1, user2, user3);
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.GetByIdsAsync(new[] { user1.Id, user3.Id }, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().Contain(new[] { user1.Id, user3.Id });
        result.Select(x => x.Id).Should().NotContain(user2.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldReturnEmpty_WhenInputEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.GetByIdsAsync(Array.Empty<Guid>(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmpty_WhenQueryEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.SearchAsync("   ", 10, Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ShouldExcludeCurrentUser_AndSearchByUserNameFirstNameLastName()
    {
        await using var db = CreateDbContext();

        var currentUser = CreateUser("current@test.com", "current", "John", "Doe");
        var byUserName = CreateUser("u1@test.com", "johnny", "Alice", "Smith");
        var byFirstName = CreateUser("u2@test.com", "alice2", "John", "Taylor");
        var byLastName = CreateUser("u3@test.com", "bob3", "Bob", "Johnson");
        var noMatch = CreateUser("u4@test.com", "zzz", "Maria", "White");

        db.Users.AddRange(currentUser, byUserName, byFirstName, byLastName, noMatch);
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.SearchAsync("john", 10, currentUser.Id, CancellationToken.None);

        result.Select(x => x.Id).Should().NotContain(currentUser.Id);
        result.Select(x => x.Id).Should().Contain(new[] { byUserName.Id, byFirstName.Id, byLastName.Id });
        result.Select(x => x.Id).Should().NotContain(noMatch.Id);
    }

    [Fact]
    public async Task SearchAsync_ShouldBeCaseInsensitive_AndOrderedByUserName()
    {
        await using var db = CreateDbContext();

        var currentUserId = Guid.NewGuid();
        var userB = CreateUser("b@test.com", "bbb", "Alpha", "One");
        var userA = CreateUser("a@test.com", "aaa", "alpha", "Two");
        var userC = CreateUser("c@test.com", "ccc", "ALPHA", "Three");

        db.Users.AddRange(userB, userA, userC);
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.SearchAsync("AlPhA", 10, currentUserId, CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(x => x.UserName).Should().ContainInOrder("aaa", "bbb", "ccc");
    }

    [Fact]
    public async Task SearchAsync_ShouldRespectLimit()
    {
        await using var db = CreateDbContext();

        db.Users.AddRange(
            CreateUser("u1@test.com", "aaa", "John", "One"),
            CreateUser("u2@test.com", "bbb", "John", "Two"),
            CreateUser("u3@test.com", "ccc", "John", "Three"));
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.SearchAsync("john", 2, Guid.NewGuid(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchByPrefixAsync_ShouldReturnEmpty_WhenPrefixEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.SearchByPrefixAsync("   ", 10, Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByPrefixAsync_ShouldReturnOnlyPrefixMatches_ExcludeCurrentUser_AndOrderByUserName()
    {
        await using var db = CreateDbContext();

        var currentUser = CreateUser("current@test.com", "alex-current", "Current", "User");
        var userB = CreateUser("b@test.com", "alex-b", "Bob", "One");
        var userA = CreateUser("a@test.com", "alex-a", "Alice", "Two");
        var noMatch = CreateUser("c@test.com", "john-c", "John", "Three");

        db.Users.AddRange(currentUser, userB, userA, noMatch);
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.SearchByPrefixAsync("AlEx", 10, currentUser.Id, CancellationToken.None);

        result.Select(x => x.Id).Should().NotContain(currentUser.Id);
        result.Select(x => x.Id).Should().Contain(new[] { userA.Id, userB.Id });
        result.Select(x => x.Id).Should().NotContain(noMatch.Id);
        result.Select(x => x.UserName).Should().ContainInOrder("alex-a", "alex-b");
    }

    [Fact]
    public async Task SearchByPrefixAsync_ShouldRespectLimit()
    {
        await using var db = CreateDbContext();

        db.Users.AddRange(
            CreateUser("u1@test.com", "alex-a", "A", "A"),
            CreateUser("u2@test.com", "alex-b", "B", "B"),
            CreateUser("u3@test.com", "alex-c", "C", "C"));
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.SearchByPrefixAsync("alex", 2, Guid.NewGuid(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByEmailsAsync_ShouldReturnEmpty_WhenInputEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.GetByEmailsAsync(Array.Empty<string>(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByEmailsAsync_ShouldReturnEmpty_WhenInputContainsOnlyBlankEmails()
    {
        await using var db = CreateDbContext();
        var repo = new UserRepository(db);

        var result = await repo.GetByEmailsAsync(new[] { "", "   ", "\t" }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByEmailsAsync_ShouldNormalizeLowercaseTrimAndDeduplicate()
    {
        await using var db = CreateDbContext();

        var user1 = CreateUser("alice@test.com", "alice", "Alice", "Smith");
        var user2 = CreateUser("bob@test.com", "bob", "Bob", "Smith");
        var user3 = CreateUser("carol@test.com", "carol", "Carol", "Smith");

        db.Users.AddRange(user1, user2, user3);
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        var result = await repo.GetByEmailsAsync(
            new[] { "  ALICE@test.com  ", "bob@test.com", "BOB@test.com", "missing@test.com" },
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Email).Should().Contain(new[] { "alice@test.com", "bob@test.com" });
        result.Select(x => x.Email).Should().NotContain("carol@test.com");
    }

    [Fact]
    public async Task GetByPhoneNumberAsync_ShouldReturn_User_WhenPhoneExists()
    {
        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        var user = User.Create(
            email: "phone_exists@test.com",
            passwordHash: "hash",
            userName: "phone_exists",
            firstName: "Phone",
            lastName: "Exists",
            phoneNumber: "+49123456789");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await repository.GetByPhoneNumberAsync("+49123456789", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Email.Should().Be("phone_exists@test.com");
        result.PhoneNumber.Should().Be("+49123456789");
    }

    [Fact]
    public async Task GetByPhoneNumberAsync_ShouldReturn_Null_WhenPhoneDoesNotExist()
    {
        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        var user = User.Create(
            email: "other_phone@test.com",
            passwordHash: "hash",
            userName: "other_phone",
            firstName: "Other",
            lastName: "Phone",
            phoneNumber: "+49111111111");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await repository.GetByPhoneNumberAsync("+49222222222", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task PhoneNumberExistsAsync_ShouldReturn_True_WhenPhoneExists()
    {
        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        var user = User.Create(
            email: "exists_phone@test.com",
            passwordHash: "hash",
            userName: "exists_phone",
            firstName: "Exists",
            lastName: "Phone",
            phoneNumber: "+497001112233");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var exists = await repository.PhoneNumberExistsAsync("+497001112233", CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task PhoneNumberExistsAsync_ShouldReturn_False_WhenPhoneDoesNotExist()
    {
        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        var user = User.Create(
            email: "false_phone@test.com",
            passwordHash: "hash",
            userName: "false_phone",
            firstName: "False",
            lastName: "Phone",
            phoneNumber: "+497009998877");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var exists = await repository.PhoneNumberExistsAsync("+490000000000", CancellationToken.None);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Users_ShouldAllow_MultipleNullPhoneNumbers()
    {
        await using var db = CreateDbContext();

        var user1 = User.Create(
            email: "null_phone_1@test.com",
            passwordHash: "hash",
            userName: "null_phone_1",
            firstName: "Null",
            lastName: "One");

        var user2 = User.Create(
            email: "null_phone_2@test.com",
            passwordHash: "hash",
            userName: "null_phone_2",
            firstName: "Null",
            lastName: "Two");

        db.Users.AddRange(user1, user2);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetByEmailsAsync_ShouldReturn_Users_ByNormalizedEmails()
    {
        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        var user1 = User.Create(
            email: "first_email@test.com",
            passwordHash: "hash",
            userName: "first_email",
            firstName: "First",
            lastName: "User");

        var user2 = User.Create(
            email: "second_email@test.com",
            passwordHash: "hash",
            userName: "second_email",
            firstName: "Second",
            lastName: "User");

        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        var result = await repository.GetByEmailsAsync(
            new[] { " FIRST_EMAIL@test.com ", "second_email@test.com" },
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Email).Should().BeEquivalentTo(new[]
        {
            "first_email@test.com",
            "second_email@test.com"
        });
    }

    [Fact]
    public async Task GetByPhoneNumberAsync_ShouldReturnNull_WhenInputIsDirtyOrEmpty()
    {
        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        var result1 = await repository.GetByPhoneNumberAsync("   ", CancellationToken.None);
        var result2 = await repository.GetByPhoneNumberAsync("+", CancellationToken.None);

        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UserRepositoryTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static User CreateUser(string email, string userName, string firstName, string lastName)
        => User.Create(
            email: email,
            passwordHash: "password-hash",
            userName: userName,
            firstName: firstName,
            lastName: lastName);
}