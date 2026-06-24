using System.Security.Cryptography;

namespace SubmissionProcessor.Worker.Helpers;

public static class CheckSumHelper
{

    public static string GetFileChecksum(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}