using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OTPFileHandler
{
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly string _rootPath;
        private readonly string _webFolder;
        private readonly DeviceHandler _deviceHandler;
        private readonly ServerHandler _serverHandler;
        private readonly List<HttpListenerContext> _sseClients = new List<HttpListenerContext>();
        private readonly object _sseLock = new object();

        public WebServer(string rootPath, DeviceHandler deviceHandler, ServerHandler serverHandler, int port = 8080)
        {
            _rootPath = rootPath;
            _webFolder = Path.Combine(_rootPath, "Web");
            Console.WriteLine($"[DEBUG] WebServer rootPath = {_rootPath}");
            Console.WriteLine($"[DEBUG] WebServer WebFolder = {_webFolder}");

            _deviceHandler = deviceHandler;
            _serverHandler = serverHandler;
            
            // Subscribe to server events for SSE
            _serverHandler.MessageDecrypted += OnMessageDecrypted;
            
            // Create HTTP listener
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
        }

        // Start the web server
        public void Start()
        {
            try
            {
                _listener.Start();
                Console.WriteLine($"Web server started at {_listener.Prefixes.First()}");
                Task.Run(() => HandleRequests()); // run the request handler
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Could not start server: {ex.Message}");
            }
        }

        private async Task HandleRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] While handling request: {ex.Message}");
                }
            }
        }

        // Stop the web server
        public void Stop()
        {
            _listener.Stop();
            Console.WriteLine("Web server stopped.");
        }

        // Process incoming HTTP requests
        private async Task ProcessRequestsAsync()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing request: {ex.Message}");
                }
            }
        }

        // Handle a single HTTP request
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath;
                
                // API endpoint handling
                if (path.StartsWith("/api/"))
                {
                    await HandleApiRequest(context);
                    return;
                }
                
                // Server-sent events endpoint
                if (path == "/events")
                {
                    await HandleEventsRequest(context);
                    return;
                }
                
                // Map root to device.html
                if (path == "/")
                {
                    path = "/device_ui.html";
                }
                
                // Map /device to device.html 
                if (path == "/device")
                {
                    path = "/device_ui.html";
                }
                
                // Map /server to server.html
                if (path == "/server")
                {
                    path = "/server_ui.html";
                }
                
                // Serve static files from web folder
                string filePath = Path.Combine(_webFolder, path.TrimStart('/'));
                
                if (File.Exists(filePath))
                {
                    string contentType = GetContentType(filePath);
                    byte[] fileContent = await File.ReadAllBytesAsync(filePath);
                    
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = fileContent.Length;
                    
                    await context.Response.OutputStream.WriteAsync(fileContent);
                    context.Response.Close();
                }
                else
                {
                    // File not found
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { /* Ignore errors in error handling */ }
            }
        }

        // Handle API requests
        private async Task HandleApiRequest(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;
            
            if (path == "/api/device/send" && context.Request.HttpMethod == "POST")
            {
                // Handle message sending from device
                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    string requestBody = await reader.ReadToEndAsync();
                    var message = JsonSerializer.Deserialize<MessageData>(requestBody);
                    Console.WriteLine($"DEBUG WebServer - Message to send: {message.MessageType},{message.Latitude},{message.Longitude},{message.AdditionalInfo}");
                    bool success = await _deviceHandler.SendMessage(
                        message.MessageType,
                        message.Latitude,
                        message.Longitude,
                        message.AdditionalInfo
                    );
                    
                    await SendJsonResponse(context, new { success });
                }
            }
            else if (path == "/api/device/pads" && context.Request.HttpMethod == "GET")
            {
                // Get device pad count
                int padCount = _deviceHandler.GetRemainingPadCount();
                await SendJsonResponse(context, new { padCount });
            }
            else if (path == "/api/server/messages" && context.Request.HttpMethod == "GET")
            {
                // Get server messages
                await _serverHandler.ForceProcessMessages();
                var messages = _serverHandler.GetDecryptedMessages();
                await SendJsonResponse(context, new { messages });
            }
            else if (path == "/api/server/pads" && context.Request.HttpMethod == "GET")
            {
                // Get server pad count
                int padCount = _serverHandler.GetRemainingPadCount();
                await SendJsonResponse(context, new { padCount });
            }
            else if (path == "/api/server/generate-pads" && context.Request.HttpMethod == "POST")
            {
                int count = int.Parse(context.Request.QueryString["count"] ?? "4");
                int size = int.Parse(context.Request.QueryString["size"] ?? "1024");

                var padManager = new PadManager(_rootPath);
                await padManager.GeneratePads(count, size);

                await SendJsonResponse(context, new { success = true, message = $"Generated {count} pads of {size} bytes." });
            }
            else if (path == "/api/server/clear-pads" && context.Request.HttpMethod == "DELETE")
            {
                var padManager = new PadManager(_rootPath);
                padManager.DeleteAllDevicePads();
                padManager.DeleteAllServerPads();

                await SendJsonResponse(context, new { success = true, message = "All pads cleared." });
            }

            else if (path == "/api/device/pad-files" && context.Request.HttpMethod == "GET")
            {
                // Get device pad files
                string padFolder = Path.Combine(_rootPath, "Device", "pads");
                var padFileInfos = new List<object>();
                
                if (Directory.Exists(padFolder))
                {
                    string[] padFiles = Directory.GetFiles(padFolder, "pad_*.bin");
                    foreach (string padFile in padFiles)
                    {
                        var fileInfo = new FileInfo(padFile);
                        padFileInfos.Add(new
                        {
                            name = Path.GetFileName(padFile),
                            size = fileInfo.Length,
                            created = fileInfo.CreationTime
                        });
                    }
                }
                
                await SendJsonResponse(context, new { files = padFileInfos });
            }

            else if (path == "/api/server/purge-orphaned-pads" && context.Request.HttpMethod == "DELETE")
            {
                // Find pads that exist on server but not on device
                string devicePadFolder = Path.Combine(_rootPath, "Device", "pads");
                string serverPadFolder = Path.Combine(_rootPath, "Server", "pads");
                
                int purgedCount = 0;
                
                try
                {
                    if (Directory.Exists(serverPadFolder))
                    {
                        var serverPadFiles = Directory.GetFiles(serverPadFolder, "pad_*.bin");
                        
                        // Get device pad filenames for comparison
                        var devicePadFilenames = new HashSet<string>();
                        if (Directory.Exists(devicePadFolder))
                        {
                            foreach (var padFile in Directory.GetFiles(devicePadFolder, "pad_*.bin"))
                            {
                                devicePadFilenames.Add(Path.GetFileName(padFile));
                            }
                        }
                        
                        // Find and delete pads on server that no longer exist on device
                        foreach (var serverPadFile in serverPadFiles)
                        {
                            string filename = Path.GetFileName(serverPadFile);
                            
                            if (!devicePadFilenames.Contains(filename))
                            {
                                // This pad exists on the server but not on the device, so it's orphaned
                                File.Delete(serverPadFile);
                                purgedCount++;
                            }
                        }
                    }
                    
                    string message = purgedCount > 0 
                        ? $"Successfully purged {purgedCount} orphaned pad(s)"
                        : "No orphaned pads found";
                    
                    await SendJsonResponse(context, new { success = true, message, purgedCount });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error purging orphaned pads: {ex.Message}");
                    await SendJsonResponse(context, new { 
                        success = false, 
                        message = $"Error purging orphaned pads: {ex.Message}" 
                    });
                }
            }

            else
            {
                // API endpoint not found
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }

        // Handle server-sent events for real-time updates
        private async Task HandleEventsRequest(HttpListenerContext context)
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.Add("Cache-Control", "no-cache");
            context.Response.Headers.Add("Connection", "keep-alive");
            
            // Keep the connection open
            lock (_sseLock)
            {
                _sseClients.Add(context);
            }
            
            try
            {
                // Send initial pad counts
                int devicePads = _deviceHandler.GetRemainingPadCount();
                int serverPads = _serverHandler.GetRemainingPadCount();
                
                await SendSSEEvent(context, "init", new
                {
                    devicePads,
                    serverPads
                });
                
                // Keep connection open
                using var cancellationTokenSource = new CancellationTokenSource();
                await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
            }
            catch (Exception)
            {
                // Client disconnected
                lock (_sseLock)
                {
                    _sseClients.Remove(context);
                }
            }
        }

        // Handle message decrypted event
        private async void OnMessageDecrypted(object sender, MessageDecryptedEventArgs e)
        {
            // Notify all SSE clients
            HttpListenerContext[] clients;
            
            lock (_sseLock)
            {
                clients = _sseClients.ToArray();
            }
            
            foreach (var client in clients)
            {
                try
                {
                    int devicePads = _deviceHandler.GetRemainingPadCount();
                    int serverPads = _serverHandler.GetRemainingPadCount();
                    
                    await SendSSEEvent(client, "message", new
                    {
                        message = e.DecryptedMessage,
                        encryptedHex = e.EncryptedHex,
                        padName = e.PadName,
                        fileName = e.FileName,
                        devicePads,
                        serverPads
                    });
                }
                catch
                {
                    // Client disconnected
                    lock (_sseLock)
                    {
                        _sseClients.Remove(client);
                    }
                }
            }
        }

        // Send a server-sent event
        private async Task SendSSEEvent(HttpListenerContext context, string eventName, object data)
        {
            string json = JsonSerializer.Serialize(data);
            string eventData = $"event: {eventName}\ndata: {json}\n\n";
            byte[] buffer = Encoding.UTF8.GetBytes(eventData);
            
            await context.Response.OutputStream.WriteAsync(buffer);
            await context.Response.OutputStream.FlushAsync();
        }

        // Send a JSON response
        private async Task SendJsonResponse(HttpListenerContext context, object data)
        {
            string json = JsonSerializer.Serialize(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.Close();
        }

        // Get MIME content type based on file extension
        private string GetContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            return extension switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        // Class for deserializing message data
        private class MessageData
        {
            public string MessageType { get; set; }
            public string Latitude { get; set; }
            public string Longitude { get; set; }
            public string AdditionalInfo { get; set; }
        }
    }
}