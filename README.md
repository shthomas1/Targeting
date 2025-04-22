# OTP Encryption System

A secure messaging system implementing one-time pad (OTP) encryption for unbreakable communication.

## Overview

This system provides a complete solution for secure message transmission using the one-time pad encryption method. It consists of a device component (sender) and a server component (receiver) that use pre-generated cryptographically secure pads to encrypt and decrypt messages.

## Features

- **Unbreakable Encryption**: Uses true one-time pad encryption, which is mathematically proven to be unbreakable when used correctly
- **Web-based UI**: Monitor both device and server activity through intuitive web interfaces
- **Real-time Updates**: Get immediate feedback when messages are sent and received
- **CSV Message Format**: Structured message format for consistent data handling
- **Automatic Timestamps**: Server automatically timestamps all received messages
- **Pad Management**: Tools to generate, monitor, and clean up encryption pads

## System Requirements

- .NET 8.0 SDK or later
- Modern web browser
- Windows, macOS, or Linux operating system

## Installation

1. Clone this repository or download the source code
2. Ensure you have the .NET 8.0 SDK installed
3. Navigate to the project directory in your terminal

## Usage

### Initial Setup

Set up the system with encryption pads:

```
dotnet run -- setup [padCount] [padSize]
```

Example:
```
dotnet run -- setup 5 4096
```
This creates 5 pads of 4KB each.

### Starting the Web Interface

Start the web server to access both device and server interfaces:

```
dotnet run -- web [port]
```

Example:
```
dotnet run -- web 8080
```

This will automatically open the web interfaces in your default browser:
- Device UI: http://localhost:8080/device_ui.html
- Server UI: http://localhost:8080/server_ui.html

### Command Line Operations

Instead of using the web interface, you can also run the components directly:

1. Run the device component (for sending messages):
```
dotnet run -- device
```

2. Run the server component (for receiving messages):
```
dotnet run -- server
```

3. Additional management commands:
```
dotnet run -- deletedevicepads   # Delete all device-side pads
dotnet run -- deleteserverpads   # Delete all server-side pads
dotnet run -- deleteallpads      # Delete all pads on both sides
```

## Message Format

Messages are formatted as CSV with the following fields:
1. Message Type (e.g., Alert, Info, Status)
2. Latitude (decimal)
3. Longitude (decimal)
4. Additional Information (text)
5. Timestamp (added by server)

Example:
```
Alert,40.7128,-74.0060,"Enemy position spotted, 3 vehicles moving north",2025-04-22 12:14:16.123
```

## Project Structure

- `DeviceHandler.cs` - Handles message sending from devices
- `ServerHandler.cs` - Processes incoming encrypted messages
- `Encryption.cs` - Core encryption/decryption logic
- `PadManager.cs` - Manages one-time pad generation and storage
- `WebServer.cs` - Provides web interface and API endpoints
- `Program.cs` - Main application entry point and CLI handling
- `Web/` - Contains HTML/JS for the web interfaces
  - `device_ui.html` & `device_ui.js` - Device monitoring interface
  - `server_ui.html` & `server_ui.js` - Server monitoring interface

## Security Considerations

- Each pad is used exactly once and then deleted
- The system ensures pads are large enough for the messages
- Pads are generated using cryptographically secure random number generation
- All used pads are immediately deleted after use

## Web Interface Features

### Device UI
- Real-time pad count display
- List of available pad files with sizes
- Statistics on total pads, used pads, and space usage
- Automatic updates when pads are consumed

### Server UI
- Real-time message display with decryption details
- Pad management tools
- Message archiving
- Orphaned pad cleanup

## Troubleshooting

- **File Locking Issues**: If you get errors about files being locked during compilation, ensure no instances of the application are running and restart your IDE.
- **Missing UI Files**: If the web interface doesn't load correctly, verify that all HTML and JS files are present in the Web folder.
- **Pad Synchronization**: If device and server pads get out of sync, use the "Purge Orphaned Pads" function in the server UI.

## How One-Time Pad Encryption Works

The one-time pad (OTP) encryption method:

1. Uses a random key (pad) that is at least as long as the message
2. Combines the message with the pad using XOR operations
3. Produces ciphertext that is mathematically impossible to decrypt without the exact same pad
4. Requires each pad to be used only once and then securely deleted
5. Depends on truly random key generation for security

## License

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

## Contributing

We welcome contributions to improve the OTP Encryption System!

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please ensure your code adheres to the same license terms (GPL v3) and includes appropriate documentation.

As a copyleft project, we believe in the freedom to use, study, share, and improve this software. By contributing, you help ensure that all derivatives of this work remain free and open source.