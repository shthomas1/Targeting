using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OTPFileHandler
{
    public class PadManager
    {
        private readonly string _devicePadFolder;
        private readonly string _serverPadFolder;

        public PadManager(string rootPath)
        {
            _devicePadFolder = Path.Combine(rootPath, "Device", "pads");
            _serverPadFolder = Path.Combine(rootPath, "Server", "pads");
            Console.WriteLine($"[DEBUG] PadManager DevicePadFolder = {_devicePadFolder}");
            Console.WriteLine($"[DEBUG] PadManager ServerPadFolder = {_serverPadFolder}");

        }

        // Generate multiple one-time pads for both device and server
        public async Task GeneratePads(int padCount, int padSize)
        {
            Console.WriteLine($"[DEBUG] Generating {padCount} pads of {padSize} bytes each...");
            Console.WriteLine($"[DEBUG] DevicePadFolder = {_devicePadFolder}");
            Console.WriteLine($"[DEBUG] ServerPadFolder = {_serverPadFolder}");

            Directory.CreateDirectory(_devicePadFolder);
            Directory.CreateDirectory(_serverPadFolder);

            for (int i = 0; i < padCount; i++)
            {
                byte[] pad = Encryption.GeneratePad(padSize);

                // Use a GUID to avoid duplicates between runs
                string padName = $"pad_{Guid.NewGuid().ToString().Substring(0, 8)}.bin";
                string devicePadPath = Path.Combine(_devicePadFolder, padName);
                string serverPadPath = Path.Combine(_serverPadFolder, padName);

                Console.WriteLine($"[DEBUG] Writing pad to DEVICE: {devicePadPath}");
                Console.WriteLine($"[DEBUG] Writing pad to SERVER: {serverPadPath}");

                await File.WriteAllBytesAsync(devicePadPath, pad);
                await File.WriteAllBytesAsync(serverPadPath, pad);
            }

            Console.WriteLine("Pad generation complete.");
        }



        // Get random pad from device folder
        public async Task<(string padName, byte[] padContent)> GetRandomDevicePad()
        {
            string[] padFiles = Directory.GetFiles(_devicePadFolder, "pad_*.bin");
            
            if (padFiles.Length == 0)
                throw new InvalidOperationException("No pads available on device");
            
            Random random = new Random();
            string selectedPadPath = padFiles[random.Next(padFiles.Length)];
            string padName = Path.GetFileName(selectedPadPath);
            
            byte[] padContent = await File.ReadAllBytesAsync(selectedPadPath);
            return (padName, padContent);
        }

        // Get all pads from server folder
        public async Task<(string padName, byte[] padContent)[]> GetAllServerPads()
        {
            string[] padFiles = Directory.GetFiles(_serverPadFolder, "pad_*.bin");
            
            if (padFiles.Length == 0)
                throw new InvalidOperationException("No pads available on server");
            
            var pads = new (string, byte[])[padFiles.Length];
            
            for (int i = 0; i < padFiles.Length; i++)
            {
                string padPath = padFiles[i];
                string padName = Path.GetFileName(padPath);
                byte[] padContent = await File.ReadAllBytesAsync(padPath);
                pads[i] = (padName, padContent);
            }
            
            return pads;
        }

        // Delete a pad from device
        public void DeleteDevicePad(string padName)
        {
            string padPath = Path.Combine(_devicePadFolder, padName);
            if (File.Exists(padPath))
            {
                File.Delete(padPath);
                Console.WriteLine($"Deleted device pad: {padName}");
            }
        }

        // Delete a pad from server
        public void DeleteServerPad(string padName)
        {
            string padPath = Path.Combine(_serverPadFolder, padName);
            if (File.Exists(padPath))
            {
                File.Delete(padPath);
                Console.WriteLine($"Deleted server pad: {padName}");
            }
        }

        // Delete all pads from device
        public void DeleteAllDevicePads()
        {
            if (Directory.Exists(_devicePadFolder))
            {
                string[] padFiles = Directory.GetFiles(_devicePadFolder, "pad_*.bin");
                foreach (string padFile in padFiles)
                {
                    File.Delete(padFile);
                }
                Console.WriteLine($"Deleted all {padFiles.Length} device pads.");
            }
            else
            {
                Console.WriteLine("Device pad folder does not exist.");
            }
        }

        // Delete all pads from server
        public void DeleteAllServerPads()
        {
            if (Directory.Exists(_serverPadFolder))
            {
                string[] padFiles = Directory.GetFiles(_serverPadFolder, "pad_*.bin");
                foreach (string padFile in padFiles)
                {
                    File.Delete(padFile);
                }
                Console.WriteLine($"Deleted all {padFiles.Length} server pads.");
            }
            else
            {
                Console.WriteLine("Server pad folder does not exist.");
            }
        }
        
        // Get count of remaining pads
        public int GetDevicePadCount()
        {
            if (!Directory.Exists(_devicePadFolder))
                return 0;
                
            return Directory.GetFiles(_devicePadFolder, "pad_*.bin").Length;
        }

        public int GetServerPadCount()
        {
            if (!Directory.Exists(_serverPadFolder))
                return 0;
                
            return Directory.GetFiles(_serverPadFolder, "pad_*.bin").Length;
        }
    }
}