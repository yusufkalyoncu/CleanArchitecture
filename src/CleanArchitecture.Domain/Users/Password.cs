using System.Security.Cryptography;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users;

public readonly record struct Password
{
    public const int MinLength = 6;
    public const int MaxLength = 30;
    
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
            return Result.Failure<Password>(PasswordErrors.CannotBeNullOrEmpty);
        }
        
        if (plainText.Length is < MinLength or > MaxLength)
        {
            return Result.Failure<Password>(PasswordErrors.InvalidLength);
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
    
    public bool VerifyPassword(string plainText)
    {
        string[] parts = HashedValue.Split('-');
        byte[] hash = Convert.FromHexString(parts[0]);
        byte[] salt = Convert.FromHexString(parts[1]);

        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, Iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(hash, inputHash);
    }
    
    public static Password FromHash(string hashedValue)
    {
        if (string.IsNullOrWhiteSpace(hashedValue))
            throw new ArgumentException("Hashed value cannot be null or empty");

        return new Password(hashedValue);
    }
}