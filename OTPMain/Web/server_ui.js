document.addEventListener('DOMContentLoaded', function() {
    const devicePadCountElement = document.getElementById('devicePadCount');
    const serverPadCountElement = document.getElementById('serverPadCount');
    const messageListElement = document.getElementById('messageList');
    const noMessagesElement = document.getElementById('noMessages');
    const refreshButton = document.getElementById('refreshButton');
    
    // Load initial data
    loadMessages();
    updatePadCounts();
    
    // Set up event source for real-time updates
    const eventSource = new EventSource('/events');
    
    eventSource.addEventListener('init', function(event) {
        const data = JSON.parse(event.data);
        updatePadCountsUI(data.devicePads, data.serverPads);
    });
    
    eventSource.addEventListener('message', function(event) {
        const data = JSON.parse(event.data);
        updatePadCountsUI(data.devicePads, data.serverPads);
        
        // Add the new message to the list
        if (data.message) {
            addMessageToList(
                data.message, 
                true, 
                data.encryptedHex, 
                data.padName, 
                data.fileName
            );
        }
    });
    
    eventSource.onerror = function() {
        console.error('EventSource failed');
        eventSource.close();
        
        // Try to reconnect after a delay
        setTimeout(function() {
            window.location.reload();
        }, 5000);
    };
    
    // Refresh button click event
    refreshButton.addEventListener('click', function() {
        loadMessages();
        updatePadCounts();
    });
    
    // Function to load messages
    function loadMessages() {
        fetch('/api/server/messages')
            .then(response => response.json())
            .then(data => {
                if (data.messages && data.messages.length > 0) {
                    // Clear the message list
                    messageListElement.innerHTML = '';
                    noMessagesElement.style.display = 'none';
                    
                    // Add each message to the list
                    data.messages.forEach(message => {
                        addMessageToList(message, false);
                    });
                } else {
                    // Show no messages message
                    messageListElement.innerHTML = '';
                    messageListElement.appendChild(noMessagesElement);
                    noMessagesElement.style.display = 'block';
                }
            })
            .catch(error => {
                console.error('Error loading messages:', error);
            });
    }
    
    // Function to update pad counts
    function updatePadCounts() {
        // Get device pad count
        fetch('/api/device/pads')
            .then(response => response.json())
            .then(data => {
                devicePadCountElement.textContent = data.padCount;
            })
            .catch(error => {
                console.error('Error fetching device pad count:', error);
                devicePadCountElement.textContent = 'Error';
            });
        
        // Get server pad count
        fetch('/api/server/pads')
            .then(response => response.json())
            .then(data => {
                serverPadCountElement.textContent = data.padCount;
            })
            .catch(error => {
                console.error('Error fetching server pad count:', error);
                serverPadCountElement.textContent = 'Error';
            });
    }
    
    // Function to update pad counts UI
    function updatePadCountsUI(deviceCount, serverCount) {
        devicePadCountElement.textContent = deviceCount;
        serverPadCountElement.textContent = serverCount;
    }
    
    // Function to add a message to the list
    function addMessageToList(message, isNew, encryptedHex = null, padName = null, fileName = null) {
        console.log("Parsing message:", message);
        
        // Special case handling for already processed messages
        // This handles the case where the message comes directly from a decrypted file
        if (message.includes(",2025-04-22")) {
            // The message is likely a complete CSV line with timestamp
            // We need to extract the pieces carefully
            
            // Try to extract each part by looking at the raw format
            let parts = [];
            let firstComma = message.indexOf(',');
            let secondComma = message.indexOf(',', firstComma + 1);
            let thirdComma = message.indexOf(',', secondComma + 1);
            let lastComma = message.lastIndexOf(',');
            
            // Extract messageType
            let messageType = message.substring(0, firstComma);
            
            // Extract latitude and longitude
            let latitude = message.substring(firstComma + 1, secondComma);
            let longitude = message.substring(secondComma + 1, thirdComma);
            
            // Extract the additional info - everything between the third comma and the timestamp
            let additionalInfo = message.substring(thirdComma + 1, lastComma);
            
            // Extract timestamp
            let timestamp = message.substring(lastComma + 1);
            
            // Create message element
            const messageElement = document.createElement('div');
            messageElement.className = 'message-item' + (isNew ? ' new-message' : '');
            
            // Add encryption details if available
            if (encryptedHex) {
                const encryptionField = document.createElement('div');
                encryptionField.className = 'message-field encryption-details';
                encryptionField.innerHTML = `<span>Encrypted:</span>${escapeHTML(encryptedHex)}`;
                messageElement.appendChild(encryptionField);
                
                const padField = document.createElement('div');
                padField.className = 'message-field pad-info';
                padField.innerHTML = `<span>Pad Used:</span>${escapeHTML(padName || 'Unknown')}`;
                messageElement.appendChild(padField);
                
                const fileField = document.createElement('div');
                fileField.className = 'message-field file-info';
                fileField.innerHTML = `<span>File:</span>${escapeHTML(fileName || 'Unknown')}`;
                messageElement.appendChild(fileField);
                
                // Add a separator
                const separator = document.createElement('hr');
                separator.className = 'message-separator';
                messageElement.appendChild(separator);
            }
            
            // Add message fields
            const typeField = document.createElement('div');
            typeField.className = 'message-field';
            typeField.innerHTML = '<span>Type:</span>' + escapeHTML(messageType);
            messageElement.appendChild(typeField);
            
            const locationField = document.createElement('div');
            locationField.className = 'message-field';
            locationField.innerHTML = '<span>Location:</span>' + escapeHTML(latitude) + ', ' + escapeHTML(longitude);
            messageElement.appendChild(locationField);
            
            const infoField = document.createElement('div');
            infoField.className = 'message-field';
            infoField.innerHTML = '<span>Info:</span>' + escapeHTML(additionalInfo);
            messageElement.appendChild(infoField);
            
            // Add timestamp field
            const timestampField = document.createElement('div');
            timestampField.className = 'message-field timestamp';
            timestampField.innerHTML = '<span>Received:</span>' + escapeHTML(timestamp);
            messageElement.appendChild(timestampField);
            
            // Hide the no messages element if it's visible
            if (noMessagesElement.parentNode === messageListElement) {
                noMessagesElement.style.display = 'none';
            }
            
            // Add to the top of the list
            messageListElement.insertBefore(messageElement, messageListElement.firstChild);
            
            return;
        }
        
        // Regular CSV handling for new messages coming from the server
        // Split by comma, but respect quoted sections
        const parts = parseCSV(message);
        console.log("Parsed parts:", parts);
        
        // Extract basic information
        const messageType = parts[0] || '';
        const latitude = parts[1] || '';
        const longitude = parts[2] || '';
        
        // For additional info and timestamp, check how many parts we have
        let additionalInfo = '';
        let timestamp = '';
        
        if (parts.length >= 5) {
            // We likely have both an additionalInfo and a timestamp
            additionalInfo = parts[3] || '';
            timestamp = parts[parts.length - 1] || '';
        } else if (parts.length === 4) {
            // We either have just additionalInfo or timestamp is included in additionalInfo
            if (parts[3].match(/^\d{4}-\d{2}-\d{2}/)) {
                // This looks like a timestamp, no additionalInfo
                timestamp = parts[3];
            } else {
                // This is additionalInfo without timestamp
                additionalInfo = parts[3];
                timestamp = new Date().toLocaleString();
            }
        } else {
            // Not enough parts, just use defaults
            timestamp = new Date().toLocaleString();
        }
        
        // Create message element
        const messageElement = document.createElement('div');
        messageElement.className = 'message-item' + (isNew ? ' new-message' : '');
        
        // Add encryption details if available
        if (encryptedHex) {
            const encryptionField = document.createElement('div');
            encryptionField.className = 'message-field encryption-details';
            encryptionField.innerHTML = `<span>Encrypted:</span>${escapeHTML(encryptedHex)}`;
            messageElement.appendChild(encryptionField);
            
            const padField = document.createElement('div');
            padField.className = 'message-field pad-info';
            padField.innerHTML = `<span>Pad Used:</span>${escapeHTML(padName || 'Unknown')}`;
            messageElement.appendChild(padField);
            
            const fileField = document.createElement('div');
            fileField.className = 'message-field file-info';
            fileField.innerHTML = `<span>File:</span>${escapeHTML(fileName || 'Unknown')}`;
            messageElement.appendChild(fileField);
            
            // Add a separator
            const separator = document.createElement('hr');
            separator.className = 'message-separator';
            messageElement.appendChild(separator);
        }
        
        // Add message fields
        const typeField = document.createElement('div');
        typeField.className = 'message-field';
        typeField.innerHTML = '<span>Type:</span>' + escapeHTML(messageType);
        messageElement.appendChild(typeField);
        
        const locationField = document.createElement('div');
        locationField.className = 'message-field';
        locationField.innerHTML = '<span>Location:</span>' + escapeHTML(latitude) + ', ' + escapeHTML(longitude);
        messageElement.appendChild(locationField);
        
        const infoField = document.createElement('div');
        infoField.className = 'message-field';
        infoField.innerHTML = '<span>Info:</span>' + escapeHTML(additionalInfo);
        messageElement.appendChild(infoField);
        
        // Add timestamp field
        const timestampField = document.createElement('div');
        timestampField.className = 'message-field timestamp';
        timestampField.innerHTML = '<span>Received:</span>' + escapeHTML(timestamp);
        messageElement.appendChild(timestampField);
        
        // Hide the no messages element if it's visible
        if (noMessagesElement.parentNode === messageListElement) {
            noMessagesElement.style.display = 'none';
        }
        
        // Add to the top of the list
        messageListElement.insertBefore(messageElement, messageListElement.firstChild);
    }
    
    // Helper function to parse CSV respecting quotes
    function parseCSV(text) {
        const result = [];
        let current = "";
        let inQuotes = false;
        
        for (let i = 0; i < text.length; i++) {
            const char = text.charAt(i);
            
            if (char === '"') {
                if (i + 1 < text.length && text.charAt(i + 1) === '"') {
                    // Double quotes inside quotes - add a single quote
                    current += '"';
                    i++;
                } else {
                    // Toggle quotes mode
                    inQuotes = !inQuotes;
                }
            } else if (char === ',' && !inQuotes) {
                // End of field
                result.push(current);
                current = "";
            } else {
                current += char;
            }
        }
        
        // Add the last field
        result.push(current);
        
        return result;
    }
    
    // Helper function to escape HTML
    function escapeHTML(str) {
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }
});

// Generate new pads
async function generatePads() {
    const count = document.getElementById('padCountInput').value || 4;
    const size = document.getElementById('padSizeInput').value || 1024;
    const statusElement = document.getElementById('padStatus');
    
    // Clear previous status
    statusElement.textContent = '';
    statusElement.className = 'status-message';
    
    // Show loading
    statusElement.textContent = 'Generating pads...';
    statusElement.className = 'status-message info';

    try {
        const res = await fetch(`/api/server/generate-pads?count=${count}&size=${size}`, {
            method: 'POST'
        });

        const result = await res.json();

        if (result.success) {
            statusElement.textContent = `✅ ${result.message}`;
            statusElement.className = 'status-message success';
            updatePadCounts(); // Immediately update the count display
        } else {
            statusElement.textContent = `❌ Pad generation failed.`;
            statusElement.className = 'status-message error';
        }
    } catch (error) {
        console.error('Error generating pads:', error);
        statusElement.textContent = `❌ Error generating pads: ${error.message}`;
        statusElement.className = 'status-message error';
    }
}

// Clear all pads (both device and server)
async function clearPads() {
    const confirmClear = confirm("Are you sure you want to delete ALL pads?");
    if (!confirmClear) return;
    
    const statusElement = document.getElementById('clearStatus');
    
    // Clear previous status
    statusElement.textContent = '';
    statusElement.className = 'status-message';
    
    // Show loading
    statusElement.textContent = 'Clearing all pads...';
    statusElement.className = 'status-message info';

    try {
        const res = await fetch('/api/server/clear-pads', { method: 'DELETE' });
        const result = await res.json();

        if (result.success) {
            statusElement.textContent = `🧼 ${result.message}`;
            statusElement.className = 'status-message success';
            updatePadCounts(); // refresh UI count
        } else {
            statusElement.textContent = `❌ Failed to clear pads.`;
            statusElement.className = 'status-message error';
        }
    } catch (error) {
        console.error('Error clearing pads:', error);
        statusElement.textContent = `❌ Error clearing pads: ${error.message}`;
        statusElement.className = 'status-message error';
    }
}

// Purge orphaned pads (used by device but not on server)
async function purgeOrphanedPads() {
    const confirmPurge = confirm("This will remove pads that were used by the device but no longer needed by the server. Continue?");
    if (!confirmPurge) return;
    
    const statusElement = document.getElementById('purgeStatus');
    
    // Clear previous status
    statusElement.textContent = '';
    statusElement.className = 'status-message';
    
    // Show loading
    statusElement.textContent = 'Purging orphaned pads...';
    statusElement.className = 'status-message info';

    try {
        const res = await fetch('/api/server/purge-orphaned-pads', { method: 'DELETE' });
        const result = await res.json();

        if (result.success) {
            statusElement.textContent = `🧹 ${result.message}`;
            statusElement.className = 'status-message success';
            updatePadCounts(); // refresh UI count
        } else {
            statusElement.textContent = `❌ Failed to purge orphaned pads.`;
            statusElement.className = 'status-message error';
        }
    } catch (error) {
        console.error('Error purging orphaned pads:', error);
        statusElement.textContent = `❌ Error purging orphaned pads: ${error.message}`;
        statusElement.className = 'status-message error';
    }
}

// Function to update pad counts (accessible from global scope)
function updatePadCounts() {
    const devicePadCountElement = document.getElementById('devicePadCount');
    const serverPadCountElement = document.getElementById('serverPadCount');
    
    // Get device pad count
    fetch('/api/device/pads')
        .then(response => response.json())
        .then(data => {
            devicePadCountElement.textContent = data.padCount;
        })
        .catch(error => {
            console.error('Error fetching device pad count:', error);
            devicePadCountElement.textContent = 'Error';
        });
    
    // Get server pad count
    fetch('/api/server/pads')
        .then(response => response.json())
        .then(data => {
            serverPadCountElement.textContent = data.padCount;
        })
        .catch(error => {
            console.error('Error fetching server pad count:', error);
            serverPadCountElement.textContent = 'Error';
        });
}