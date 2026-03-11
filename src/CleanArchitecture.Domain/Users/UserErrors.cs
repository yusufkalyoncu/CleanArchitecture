using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public static class UserErrors
{
    private const string LengthKey = "Length";

    public static readonly Error AlreadyExists = Error.Conflict("User.AlreadyExists");
    public static readonly Error InvalidCredentials = Error.Unauthorized("User.InvalidCredentials");

    public static class Email
    {
        public static readonly Error Empty = Error.BadRequest("User.Email.Empty");
        public static readonly Error InvalidFormat = Error.BadRequest("User.Email.InvalidFormat");
        public static readonly Error TooLong = Error.BadRequest("User.Email.LengthExceeded");
    }

    public static class Name
    {
        public static class FirstName
        {
            public static readonly Error Empty = Error.BadRequest("User.FirstName.Empty");

            public static readonly Error TooShort = Error.BadRequest("User.FirstName.TooShort",
                new Dictionary<string, object?>
                {
                    { LengthKey, Users.Name.FirstNameMinLength }
                });

            public static readonly Error TooLong = Error.BadRequest("User.FirstName.TooLong",
                new Dictionary<string, object?>
                {
                    { LengthKey, Users.Name.FirstNameMaxLength }
                });
        }

        public static class LastName
        {
            public static readonly Error Empty = Error.BadRequest("User.LastName.Empty");

            public static readonly Error TooShort = Error.BadRequest("User.LastName.TooShort",
                new Dictionary<string, object?>
                {
                    { LengthKey, Users.Name.LastNameMinLength }
                });

            public static readonly Error TooLong = Error.BadRequest("User.LastName.TooLong",
                new Dictionary<string, object?>
                {
                    { LengthKey, Users.Name.LastNameMaxLength }
                });
        }
    }

    public static class Password
    {
        public static readonly Error Empty = Error.BadRequest("User.Password.Empty");

        public static readonly Error TooShort = Error.BadRequest("User.Password.TooShort",
            new Dictionary<string, object?>
            {
                { LengthKey, Users.Password.MinLength }
            });

        public static readonly Error TooLong = Error.BadRequest("User.Password.TooLong",
            new Dictionary<string, object?>
            {
                { LengthKey, Users.Password.MaxLength }
            });
    }

    public static class Auth
    {
        public static readonly Error InvalidToken = Error.Unauthorized("User.Auth.InvalidToken");
        public static readonly Error MaxSessionsReached = Error.TooManyRequests("User.Auth.MaxSessionsReached");
        public static readonly Error TokenOnCooldown = Error.TooManyRequests("User.Auth.TokenOnCooldown");
    }
}