using System.Security.Cryptography;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public readonly record struct Password
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 500000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;
    
    public string HashedValue { get; }
    
    private Password(string hashedValue) => HashedValue = hashedValue;

    public static Result<Password> Create(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return Result.Failure<Password>(PasswordErrors.PasswordCannotBeNullOrEmpty);
        }
        
        if (plainText.Length < 6)
        {
            return Result.Failure<Password>(PasswordErrors.PasswordLengthInvalid);
        }
        
        var hashedValue = HashPassword(plainText);
        return new Password(hashedValue);
    }
    
    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return $"{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";
    }
    
    private static bool VerifyPassword(string password, string passwordHash)
    {
        string[] parts = passwordHash.Split('-');
        byte[] hash = Convert.FromHexString(parts[0]);
        byte[] salt = Convert.FromHexString(parts[1]);

        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(hash, inputHash);
    }
    
    public static Password FromHash(string hashedValue)
    {
        if (string.IsNullOrWhiteSpace(hashedValue))
            throw new ArgumentException("Hashed value cannot be null or empty");

        return new Password(hashedValue);
    }
}