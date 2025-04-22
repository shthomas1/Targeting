using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OTPFileHandler
{
    public static class Encryption
    {
        // Generate a cryptographically secure one-time pad
        public static byte[] GeneratePad(int size)
        {
            byte[] pad = new byte[size];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(pad);
            }
            return pad;
        }

        // Encrypt data using XOR with one-time pad
        public static byte[] Encrypt(byte[] data, byte[] pad)
        {
            if (pad.Length < data.Length)
                throw new ArgumentException("Pad must be at least as long as the data", nameof(pad));

            byte[] encrypted = new byte[data.Length];
            
            for (int i = 0; i < data.Length; i++)
            {
                encrypted[i] = (byte)(data[i] ^ pad[i]);
            }
            
            return encrypted;
        }

        // Decrypt data using XOR with one-time pad (same as encryption)
        public static byte[] Decrypt(byte[] encrypted, byte[] pad)
        {
            return Encrypt(encrypted, pad); // XOR operation is symmetric
        }

        // Validate that a decrypted message is in the expected CSV format
        public static bool ValidateDecryption(byte[] decrypted)
        {
            try
            {
                string message = Encoding.UTF8.GetString(decrypted);
                
                // Check if it's a valid CSV format
                string[] parts = message.Split(',');
                
                // Basic check: A valid message should have at least 4 parts:
                // MessageType, Latitude, Longitude, Additional Information
                if (parts.Length < 4)
                    return false;

                // Try to parse latitude and longitude as doubles
                if (!double.TryParse(parts[1], out _) || !double.TryParse(parts[2], out _))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}