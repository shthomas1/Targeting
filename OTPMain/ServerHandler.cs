using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Linq;
using System.Text.RegularExpressions;

namespace OTPFileHandler
{
    public class MessageDecryptedEventArgs : EventArgs
    {
        public string DecryptedMessage { get; }
        public string EncryptedHex { get; }
        public string PadName { get; }
        public string FileName { get; }

        public MessageDecryptedEventArgs(string decryptedMessage, string encryptedHex, string padName, string fileName)
        {
            DecryptedMessage = decryptedMessage;
            EncryptedHex = encryptedHex;
            PadName = padName;
            FileName = fileName;
        }
    }

    public class ServerHandler
    {
        private readonly string _rootPath;
        private readonly string _serverIncomingFolder;
        private readonly string _serverDecryptedFolder;
        private readonly PadManager _padManager;
        private readonly System.Timers.Timer _processingTimer;
        private readonly List<string> _decryptedMessages = new List<string>();
        private readonly HashSet<string> _failedMessages = new HashSet<string>();

        public event EventHandler<MessageDecryptedEventArgs> MessageDecrypted;

        public ServerHandler(string rootPath, PadManager padManager)
        {
            _rootPath = rootPath;
            _serverIncomingFolder = Path.Combine(rootPath, "Server", "incoming");
            _serverDecryptedFolder = Path.Combine(rootPath, "Server", "decrypted");
            _padManager = padManager;

            Directory.CreateDirectory(_serverIncomingFolder);
            Directory.CreateDirectory(_serverDecryptedFolder);

            LoadExistingDecryptedMessages();

            _processingTimer = new System.Timers.Timer(2000);
            _processingTimer.Elapsed += async (s, e) => await ProcessIncomingMessages();
            _processingTimer.AutoReset = true;
        }

        private void LoadExistingDecryptedMessages()
        {
            try
            {
                string[] files = Directory.GetFiles(_serverDecryptedFolder, "*.csv");
                foreach (string file in files)
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        if (!string.IsNullOrEmpty(content))
                        {
                            _decryptedMessages.Add(content);
                            Console.WriteLine($"Loaded existing decrypted message: {content}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading decrypted message {file}: {ex.Message}");
                    }
                }
                Console.WriteLine($"Loaded {_decryptedMessages.Count} existing decrypted messages.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading existing decrypted messages: {ex.Message}");
            }
        }

        public void Start()
        {
            _processingTimer.Start();
            Console.WriteLine("Server processing started.");
        }

        public void Stop()
        {
            _processingTimer.Stop();
            Console.WriteLine("Server processing stopped.");
        }

        private async Task ProcessIncomingMessages()
        {
            try
            {
                string[] messageFiles = Directory.GetFiles(_serverIncomingFolder, "*.bin");

                foreach (string messageFile in messageFiles)
                {
                    string fileName = Path.GetFileName(messageFile);
                    if (_failedMessages.Contains(fileName)) continue;
                    await ProcessMessage(messageFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing messages: {ex.Message}");
            }
        }

        private async Task ProcessMessage(string messageFilePath)
        {
            try
            {
                string fileName = Path.GetFileName(messageFilePath);
                byte[] encryptedData = await File.ReadAllBytesAsync(messageFilePath);

                Console.WriteLine($"\n------ PROCESSING ENCRYPTED MESSAGE ------");
                Console.WriteLine($"Filename: {fileName}");
                Console.WriteLine($"Encrypted data size: {encryptedData.Length} bytes");
                Console.WriteLine($"Encrypted data (hex): {BitConverter.ToString(encryptedData.Take(32).ToArray())}...");

                string padFolder = Path.Combine(_rootPath, "Server", "pads");
                string[] padFiles = Directory.GetFiles(padFolder, "pad_*.bin");

                if (padFiles.Length == 0)
                {
                    Console.WriteLine("No pads available in Server/pads for decryption.");
                    return;
                }

                Console.WriteLine($"Attempting decryption with {padFiles.Length} available pads...");
                bool decryptionSuccessful = false;

                foreach (string padFilePath in padFiles)
                {
                    string padName = Path.GetFileName(padFilePath);
                    byte[] padContent = await File.ReadAllBytesAsync(padFilePath);

                    if (padContent.Length < encryptedData.Length)
                    {
                        Console.WriteLine($"Pad {padName} is too small for message {fileName}");
                        continue;
                    }

                    byte[] decryptedData = Encryption.Decrypt(encryptedData, padContent);

                    if (Encryption.ValidateDecryption(decryptedData))
                    {
                        string decryptedMessage = Encoding.UTF8.GetString(decryptedData);
                        
                        // Add timestamp to the decrypted message
                        string timestampedMessage = AddTimestampToMessage(decryptedMessage);

                        Console.WriteLine($"\n------ DECRYPTION SUCCESSFUL ------");
                        Console.WriteLine($"Used pad: {padName}");
                        Console.WriteLine($"Decrypted message: {timestampedMessage}");

                        string decryptedFilePath = Path.Combine(_serverDecryptedFolder, $"{Path.GetFileNameWithoutExtension(fileName)}.csv");
                        await File.WriteAllTextAsync(decryptedFilePath, timestampedMessage);

                        File.Delete(padFilePath);
                        Console.WriteLine($"Deleted pad: {padName}");

                        File.Delete(messageFilePath);
                        Console.WriteLine($"Deleted encrypted message: {fileName}");

                        _failedMessages.Remove(fileName);
                        _decryptedMessages.Add(timestampedMessage);

                        string encryptedHex = BitConverter.ToString(encryptedData.Take(32).ToArray()) + "...";

                        MessageDecrypted?.Invoke(this, new MessageDecryptedEventArgs(
                            timestampedMessage, encryptedHex, padName, fileName));

                        decryptionSuccessful = true;
                        break;
                    }
                }

                if (!decryptionSuccessful)
                {
                    Console.WriteLine($"\n------ DECRYPTION FAILED ------");
                    Console.WriteLine($"Could not decrypt message {fileName} with any available pad.");
                    _failedMessages.Add(fileName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message {messageFilePath}: {ex.Message}");
            }
        }

        // Method to add timestamp to CSV message
        private string AddTimestampToMessage(string message)
        {
            try
            {
                // Parse CSV fields properly, handling escaped fields
                List<string> fields = ParseCsvLine(message);
                
                // Get current timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                
                // Add timestamp to the end of the fields
                fields.Add(timestamp);
                
                // Join fields back into CSV format
                return string.Join(",", fields);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding timestamp to message: {ex.Message}");
                // Return original message if there's an error
                return message;
            }
        }
        
        // Parse CSV line correctly, handling quoted fields with commas
        private List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            StringBuilder currentField = new StringBuilder();
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '\"')
                {
                    // Check if it's an escaped quote
                    if (i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        // Add a single quote to the field
                        currentField.Append('\"');
                        i++; // Skip the next quote
                    }
                    else
                    {
                        // Toggle quote state
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // End of field
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    // Add character to current field
                    currentField.Append(c);
                }
            }
            
            // Add the last field
            fields.Add(currentField.ToString());
            
            return fields;
        }

        public List<string> GetDecryptedMessages()
        {
            if (_decryptedMessages.Count == 0)
            {
                LoadExistingDecryptedMessages();
            }
            return new List<string>(_decryptedMessages);
        }

        public async Task ForceProcessMessages()
        {
            _failedMessages.Clear();
            await ProcessIncomingMessages();
        }

        public int GetRemainingPadCount()
        {
            return _padManager.GetServerPadCount();
        }
    }
}