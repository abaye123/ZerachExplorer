using System;
using System.Security.Cryptography;

namespace ZerachExplorer.Helpers;

public static class SecurityHelper
{
    private const int SaltByteSize = 24;
    private const int HashByteSize = 32;
    private const int Iterations = 600_000;

    public static void HashPassword(string password, out string hash, out string salt)
    {
        if (password == null) password = string.Empty;

        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltByteSize);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, HashByteSize);

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

            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, expectedHashBytes.Length);

            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
