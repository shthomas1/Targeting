using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OTPFileHandler
{
    public class DeviceHandler
    {
        private readonly string _rootPath;
        private readonly string _devicePadFolder;
        private readonly string _serverIncomingFolder;
        private readonly PadManager _padManager;

        public DeviceHandler(string rootPath, PadManager padManager)
        {
            _rootPath = rootPath;
            _devicePadFolder = Path.Combine(rootPath, "Device", "pads");
            _serverIncomingFolder = Path.Combine(rootPath, "Server", "incoming");
            _padManager = padManager;

            // Ensure folders exist
            Directory.CreateDirectory(_devicePadFolder);
            Directory.CreateDirectory(_serverIncomingFolder);
        }

        // Send encrypted message from device to server
        public async Task<bool> SendMessage(string messageType, string latitude, string longitude, string additionalInfo)
        {
            try
            {
                // Properly escape fields that might contain commas
                string escapedMessageType = EscapeField(messageType);
                string escapedLatitude = EscapeField(latitude);
                string escapedLongitude = EscapeField(longitude);
                string escapedAdditionalInfo = EscapeField(additionalInfo);

                // Format the message as CSV
                string message = $"{escapedMessageType},{escapedLatitude},{escapedLongitude},{escapedAdditionalInfo}";
                
                Console.WriteLine($"Preparing to send message: {message}");
                
                // Get a random pad
                var (padName, padContent) = await _padManager.GetRandomDevicePad();
                
                // Encrypt the message
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                
                if (padContent.Length < messageBytes.Length)
                {
                    Console.WriteLine("Error: Message is larger than the available pad.");
                    return false;
                }
                
                byte[] encryptedMessage = Encryption.Encrypt(messageBytes, padContent);
                
                // Save encrypted message to server incoming folder
                string fileName = $"msg_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
                string filePath = Path.Combine(_serverIncomingFolder, fileName);
                
                await File.WriteAllBytesAsync(filePath, encryptedMessage);
                Console.WriteLine($"Encrypted message saved to: {filePath}");
                
                // Delete the used pad
                _padManager.DeleteDevicePad(padName);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
                return false;
            }
        }

        // Escape a field that might contain commas
        private string EscapeField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // If field contains commas, quotes, or newlines, wrap it in quotes and escape any quotes
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                // Replace any double quotes with two double quotes
                field = field.Replace("\"", "\"\"");
                // Wrap the field in double quotes
                return $"\"{field}\"";
            }

            return field;
        }

        // Get remaining pad count
        public int GetRemainingPadCount()
        {
            return _padManager.GetDevicePadCount();
        }
    }
}