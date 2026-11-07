using System;
using System.Security.Cryptography;

namespace LiteExplorer.Helpers;

public static class SecurityHelper
{
    private const int SaltByteSize = 24;
    private const int HashByteSize = 32;
    private const int Iterations = 10000;

    public static void HashPassword(string password, out string hash, out string salt)
    {
        if (password == null) password = string.Empty;

        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltByteSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
        byte[] hashBytes = pbkdf2.GetBytes(HashByteSize);

        salt = Convert.ToBase64String(saltBytes);
        hash = Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            return false;

        if (password == null) password = string.Empty;

        try
        {
            byte[] saltBytes = Convert.FromBase64String(storedSalt);
            byte[] expectedHashBytes = Convert.FromBase64String(storedHash);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
            byte[] actualHashBytes = pbkdf2.GetBytes(expectedHashBytes.Length);

            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }
        catch
        {
            return false;
        }
    }
}
