using System;
using System.IO;
using System.Threading.Tasks;

namespace OTPFileHandler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string rootPath = Directory.GetCurrentDirectory();
            Console.WriteLine($"[DEBUG] rootPath = {rootPath}");

            
            if (args.Length > 0)
            {
                string command = args[0].ToLowerInvariant();
                
                switch (command)
                {
                    case "setup":
                        await SetupSystem(rootPath, args);
                        break;
                    
                    case "device":
                        RunDevice(rootPath);
                        break;
                    
                    case "server":
                        RunServer(rootPath);
                        break;
                    
                    case "web":
                        await RunWebServer(rootPath, args);
                        break;
                    
                    case "deletedevicepads":
                        DeleteDevicePads(rootPath);
                        break;
                        
                    case "deleteserverpads":
                        DeleteServerPads(rootPath);
                        break;
                    
                    case "deleteallpads":
                        DeleteAllPads(rootPath);
                        break;
                    
                    default:
                        PrintUsage();
                        break;
                }
            }
            else
            {
                PrintUsage();
            }
        }

        // Set up the system structure and generate pads
        static async Task SetupSystem(string rootPath, string[] args)
        {
            int padCount = 5; // Default number of pads
            int padSize = 4096; // Default pad size (4KB)
            
            // Parse arguments
            if (args.Length > 1 && int.TryParse(args[1], out int count))
                padCount = count;
                
            if (args.Length > 2 && int.TryParse(args[2], out int size))
                padSize = size;
            
            Console.WriteLine($"Setting up system at {rootPath}");
            
            // Create main folder structure
            CreateDirectoryStructure(rootPath);
            
            // Create pad manager
            var padManager = new PadManager(rootPath);
            
            // Generate pads
            await padManager.GeneratePads(padCount, padSize);
            
            Console.WriteLine("System setup complete.");
            Console.WriteLine($"Generated {padCount} pads of {padSize} bytes each.");
            Console.WriteLine();
            Console.WriteLine("To start the web server:");
            Console.WriteLine("  dotnet run -- web 8080");
            Console.WriteLine();
            Console.WriteLine("To run the device component:");
            Console.WriteLine("  dotnet run -- device");
            Console.WriteLine();
            Console.WriteLine("To run the server component:");
            Console.WriteLine("  dotnet run -- server");
        }

        // Run the device component
        static void RunDevice(string rootPath)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  OTP ENCRYPTION SYSTEM - DEVICE");
            Console.WriteLine("====================================");
            Console.WriteLine("\nThis component allows you to send encrypted messages using one-time pads.");
            Console.WriteLine("Each message sent will consume one one-time pad.");
            
            var padManager = new PadManager(rootPath);
            var deviceHandler = new DeviceHandler(rootPath, padManager);
            
            int padCount = deviceHandler.GetRemainingPadCount();
            Console.WriteLine($"\nDevice has {padCount} one-time pads available.");
            
            if (padCount == 0)
            {
                Console.WriteLine("\nWARNING: No pads available. Run 'dotnet run -- setup' to generate new pads.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            
            // Simple console interface
            bool running = true;
            
            while (running)
            {
                Console.WriteLine("\n------ DEVICE MENU ------");
                Console.WriteLine($"Remaining pads: {deviceHandler.GetRemainingPadCount()}");
                Console.WriteLine("1. Send encrypted message");
                Console.WriteLine("0. Exit");
                Console.Write("\nEnter choice [0-1]: ");
                
                string choice = Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        SendMessage(deviceHandler);
                        break;
                    
                    case "0":
                        running = false;
                        break;
                    
                    default:
                        Console.WriteLine("Invalid choice. Please enter 0 or 1.");
                        break;
                }
            }
        }

        // Helper to send a message from the console
        static async void SendMessage(DeviceHandler deviceHandler)
        {
            try
            {
                Console.WriteLine("\n------ NEW MESSAGE ------");
                Console.WriteLine("Enter message details in the following format:");
                
                Console.Write("Message Type (e.g., Alert, Info, Status): ");
                string messageType = Console.ReadLine();
                
                Console.Write("Latitude (decimal number, e.g., 40.7128): ");
                string latitude = Console.ReadLine();
                if (!double.TryParse(latitude, out _))
                {
                    Console.WriteLine("Warning: Latitude should be a valid decimal number.");
                }
                
                Console.Write("Longitude (decimal number, e.g., -74.0060): ");
                string longitude = Console.ReadLine();
                if (!double.TryParse(longitude, out _))
                {
                    Console.WriteLine("Warning: Longitude should be a valid decimal number.");
                }
                
                Console.Write("Additional Information: ");
                string additionalInfo = Console.ReadLine();
                
                Console.WriteLine("\nSending message with format:");
                Console.WriteLine($"{messageType},{latitude},{longitude},{additionalInfo}");
                Console.WriteLine();
                
                bool success = await deviceHandler.SendMessage(messageType, latitude, longitude, additionalInfo);
                
                if (success)
                    Console.WriteLine("Message sent successfully.");
                else
                    Console.WriteLine("Failed to send message.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // Run the server component
        static void RunServer(string rootPath)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  OTP ENCRYPTION SYSTEM - SERVER");
            Console.WriteLine("====================================");
            Console.WriteLine("\nThis component receives and decrypts messages using one-time pads.");
            Console.WriteLine("The server will automatically attempt to decrypt incoming messages.");
            Console.WriteLine("Each successful decryption will consume one one-time pad.");
            
            var padManager = new PadManager(rootPath);
            var serverHandler = new ServerHandler(rootPath, padManager);
            
            // Start message processing
            serverHandler.Start();
            
            // Subscribe to new message events
            serverHandler.MessageDecrypted += (sender, eventArgs) =>
            {
                Console.WriteLine("\n------ NEW MESSAGE DECRYPTED ------");
                Console.WriteLine($"Encrypted data (hex): {eventArgs.EncryptedHex}");
                Console.WriteLine($"Used one-time pad: {eventArgs.PadName}");
                Console.WriteLine($"Original message filename: {eventArgs.FileName}");
                Console.WriteLine("---------------------------------");
                
                string[] parts = eventArgs.DecryptedMessage.Split(',', 4);
                if (parts.Length >= 4)
                {
                    Console.WriteLine($"Type:      {parts[0]}");
                    Console.WriteLine($"Latitude:  {parts[1]}");
                    Console.WriteLine($"Longitude: {parts[2]}");
                    Console.WriteLine($"Info:      {parts[3]}");
                }
                else
                {
                    Console.WriteLine(eventArgs.DecryptedMessage);
                }
                Console.WriteLine("---------------------------------");
            };
            
            int padCount = serverHandler.GetRemainingPadCount();
            Console.WriteLine($"\nServer has {padCount} one-time pads available.");
            
            if (padCount == 0)
            {
                Console.WriteLine("\nWARNING: No pads available. Run 'dotnet run -- setup' to generate new pads.");
            }
            
            // Simple console interface
            bool running = true;
            
            while (running)
            {
                Console.WriteLine("\n------ SERVER MENU ------");
                Console.WriteLine($"Remaining pads: {serverHandler.GetRemainingPadCount()}");
                Console.WriteLine($"Decrypted messages: {serverHandler.GetDecryptedMessages().Count}");
                Console.WriteLine("1. Force process incoming messages");
                Console.WriteLine("2. List all decrypted messages");
                Console.WriteLine("0. Exit");
                Console.Write("\nEnter choice [0-2]: ");
                
                string choice = Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        Console.WriteLine("\nProcessing incoming messages...");
                        serverHandler.ForceProcessMessages().Wait();
                        Console.WriteLine("Processing complete.");
                        break;
                    
                    case "2":
                        ListDecryptedMessages(serverHandler);
                        break;
                    
                    case "0":
                        Console.WriteLine("\nStopping server...");
                        serverHandler.Stop();
                        running = false;
                        break;
                    
                    default:
                        Console.WriteLine("Invalid choice. Please enter 0, 1, or 2.");
                        break;
                }
            }
        }

        // Helper to list decrypted messages
        static void ListDecryptedMessages(ServerHandler serverHandler)
        {
            var messages = serverHandler.GetDecryptedMessages();
            
            if (messages.Count == 0)
            {
                Console.WriteLine("\nNo decrypted messages available.");
                return;
            }
            
            Console.WriteLine($"\n------ DECRYPTED MESSAGES ({messages.Count}) ------");
            
            for (int i = 0; i < messages.Count; i++)
            {
                Console.WriteLine($"\nMessage #{i + 1}:");
                Console.WriteLine("---------------------");
                
                string[] parts = messages[i].Split(',', 4); // Split into at most 4 parts
                if (parts.Length >= 4)
                {
                    Console.WriteLine($"Type:      {parts[0]}");
                    Console.WriteLine($"Latitude:  {parts[1]}");
                    Console.WriteLine($"Longitude: {parts[2]}");
                    Console.WriteLine($"Info:      {parts[3]}");
                }
                else
                {
                    // Fallback if the message format isn't as expected
                    Console.WriteLine(messages[i]);
                }
                Console.WriteLine("---------------------");
            }
        }
        
        // Run the web server
        static async Task RunWebServer(string rootPath, string[] args)
        {
            int port = 8080; // Default port
            
            // Parse port argument
            if (args.Length > 1 && int.TryParse(args[1], out int p))
                port = p;
            
            Console.WriteLine("====================================");
            Console.WriteLine("  OTP ENCRYPTION SYSTEM - WEB SERVER");
            Console.WriteLine("====================================");
            Console.WriteLine("\nStarting web server on port " + port + "...");
            
            var padManager = new PadManager(rootPath);
            var deviceHandler = new DeviceHandler(rootPath, padManager);
            var serverHandler = new ServerHandler(rootPath, padManager);
            var webServer = new WebServer(rootPath, deviceHandler, serverHandler, port);
            
            // Create the Web folder if it doesn't exist
            string webFolder = Path.Combine(rootPath, "Web");
            if (!Directory.Exists(webFolder))
            {
                Console.WriteLine("Warning: Web folder not found. Creating empty folder structure.");
                Directory.CreateDirectory(webFolder);
            }
            
            // Check for HTML files
            string deviceHtmlPath = Path.Combine(webFolder, "device_ui.html");
            string serverHtmlPath = Path.Combine(webFolder, "server_ui.html");
            
            if (!File.Exists(deviceHtmlPath) || !File.Exists(serverHtmlPath))
            {
                Console.WriteLine("Warning: Web UI files not found. The web interface may not work correctly.");
                Console.WriteLine("Ensure the following files exist in the Web folder:");
                Console.WriteLine("  - device_ui.html");
                Console.WriteLine("  - device_ui.js");
                Console.WriteLine("  - server_ui.html");
                Console.WriteLine("  - server_ui.js");
            }
            
            int devicePads = deviceHandler.GetRemainingPadCount();
            int serverPads = serverHandler.GetRemainingPadCount();
            
            // Start the server and message processing
            serverHandler.Start();
            webServer.Start();

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"http://localhost:{port}/device_ui.html",
                    UseShellExecute = true
                });

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"http://localhost:{port}/server_ui.html",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Could not open browser: {ex.Message}");
            }


            Console.WriteLine("\n------ WEB SERVER STARTED ------");
            Console.WriteLine($"Device has {devicePads} one-time pads available.");
            Console.WriteLine($"Server has {serverPads} one-time pads available.");

            if (devicePads == 0 || serverPads == 0)
            {
                Console.WriteLine("\nWARNING: One or more components have no pads available.");
                Console.WriteLine("Run 'dotnet run -- setup' to generate new pads.");
            }

            Console.WriteLine("\nWeb interfaces available at:");
            Console.WriteLine($"  Device UI: http://localhost:{port}/device_ui.html");
            Console.WriteLine($"  Server UI: http://localhost:{port}/server_ui.html");

            // Just hang the thread to keep the server alive
            Console.WriteLine("Press Ctrl+C to stop the server.");
            await Task.Delay(-1);

        }
        
        // Delete all device pads
        static void DeleteDevicePads(string rootPath)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  DELETE DEVICE PADS");
            Console.WriteLine("====================================");
            
            var padManager = new PadManager(rootPath);
            int padCount = padManager.GetDevicePadCount();
            
            if (padCount == 0)
            {
                Console.WriteLine("No device pads found to delete.");
                return;
            }
            
            Console.WriteLine($"WARNING: This will delete all {padCount} pads on the device side.");
            Console.Write("Are you sure you want to continue? (y/n): ");
            
            string response = Console.ReadLine()?.ToLower();
            
            if (response == "y" || response == "yes")
            {
                padManager.DeleteAllDevicePads();
                Console.WriteLine("All device pads have been deleted.");
            }
            else
            {
                Console.WriteLine("Operation cancelled.");
            }
        }
        
        // Delete all server pads
        static void DeleteServerPads(string rootPath)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  DELETE SERVER PADS");
            Console.WriteLine("====================================");
            
            var padManager = new PadManager(rootPath);
            int padCount = padManager.GetServerPadCount();
            
            if (padCount == 0)
            {
                Console.WriteLine("No server pads found to delete.");
                return;
            }
            
            Console.WriteLine($"WARNING: This will delete all {padCount} pads on the server side.");
            Console.Write("Are you sure you want to continue? (y/n): ");
            
            string response = Console.ReadLine()?.ToLower();
            
            if (response == "y" || response == "yes")
            {
                padManager.DeleteAllServerPads();
                Console.WriteLine("All server pads have been deleted.");
            }
            else
            {
                Console.WriteLine("Operation cancelled.");
            }
        }
        
        // Delete all pads (both device and server)
        static void DeleteAllPads(string rootPath)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  DELETE ALL PADS");
            Console.WriteLine("====================================");
            
            var padManager = new PadManager(rootPath);
            int devicePadCount = padManager.GetDevicePadCount();
            int serverPadCount = padManager.GetServerPadCount();
            int totalPadCount = devicePadCount + serverPadCount;
            
            if (totalPadCount == 0)
            {
                Console.WriteLine("No pads found to delete.");
                return;
            }
            
            Console.WriteLine($"WARNING: This will delete all {devicePadCount} device pads and {serverPadCount} server pads.");
            Console.Write("Are you sure you want to continue? (y/n): ");
            
            string response = Console.ReadLine()?.ToLower();
            
            if (response == "y" || response == "yes")
            {
                padManager.DeleteAllDevicePads();
                padManager.DeleteAllServerPads();
                Console.WriteLine("All pads have been deleted.");
            }
            else
            {
                Console.WriteLine("Operation cancelled.");
            }
        }
        
        // Create the directory structure
        static void CreateDirectoryStructure(string rootPath)
        {
            // Create main directories
            Directory.CreateDirectory(rootPath);
            
            // Create device directories
            Directory.CreateDirectory(Path.Combine(rootPath, "Device"));
            Directory.CreateDirectory(Path.Combine(rootPath, "Device", "pads"));
            Directory.CreateDirectory(Path.Combine(rootPath, "Device", "outgoing"));
            
            // Create server directories
            Directory.CreateDirectory(Path.Combine(rootPath, "Server"));
            Directory.CreateDirectory(Path.Combine(rootPath, "Server", "pads"));
            Directory.CreateDirectory(Path.Combine(rootPath, "Server", "incoming"));
            Directory.CreateDirectory(Path.Combine(rootPath, "Server", "decrypted"));
        }
        
        // Print usage information
        static void PrintUsage()
        {
            Console.WriteLine("OTP Encryption System");
            Console.WriteLine("====================");
            Console.WriteLine("\nCOMMANDS:");
            Console.WriteLine("  setup [padCount] [padSize]  - Set up the system with the specified number and size of pads");
            Console.WriteLine("  device                      - Run the device component");
            Console.WriteLine("  server                      - Run the server component");
            Console.WriteLine("  web [port]                  - Run the web server on the specified port");
            Console.WriteLine("  deletedevicepads            - Delete all one-time pads on the device side");
            Console.WriteLine("  deleteserverpads            - Delete all one-time pads on the server side");
            Console.WriteLine("  deleteallpads               - Delete all one-time pads on both device and server");
            
            Console.WriteLine("\nEXAMPLES:");
            Console.WriteLine("  dotnet run -- setup 5 4096  - Set up with 5 pads of 4KB each");
            Console.WriteLine("  dotnet run -- web 8080      - Run the web server on port 8080");
            Console.WriteLine("  dotnet run -- deleteallpads - Delete all one-time pads");
            
            Console.WriteLine("\nINPUT FORMAT FOR DEVICE MESSAGES:");
            Console.WriteLine("  When using the device component, you'll be asked to enter:");
            Console.WriteLine("  - Message Type: A category identifier (e.g., Alert, Info, Status)");
            Console.WriteLine("  - Latitude: A decimal number (e.g., 40.7128)");
            Console.WriteLine("  - Longitude: A decimal number (e.g., -74.0060)");
            Console.WriteLine("  - Additional Information: Free-form text, can include spaces and special characters");
            
            Console.WriteLine("\nMESSAGE FORMAT:");
            Console.WriteLine("  The message will be formatted as: MessageType,Latitude,Longitude,Additional Information");
            Console.WriteLine("  Example: Alert,40.7128,-74.0060,Target location is secure");
        }
    }
}