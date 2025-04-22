document.addEventListener('DOMContentLoaded', function() {
    const padCountElement = document.getElementById('padCount');
    const fileListElement = document.getElementById('fileList');
    const noFilesElement = document.getElementById('noFiles');
    const refreshButton = document.getElementById('refreshButton');
    const totalPadsElement = document.getElementById('totalPads');
    const usedPadsElement = document.getElementById('usedPads');
    const padSizeElement = document.getElementById('padSize');
    
    // Track pad usage statistics
    let padStats = {
        total: 0,    // Total pads created
        used: 0,     // Pads consumed
        size: 0      // Average pad size in bytes
    };
    
    // Load initial pad stats from localStorage or set defaults
    const storedStats = localStorage.getItem('otp_device_pad_stats');
    if (storedStats) {
        try {
            padStats = JSON.parse(storedStats);
        } catch (e) {
            console.error('Error parsing stored pad stats:', e);
        }
    }
    
    // Initialize pad files array
    let padFiles = [];
    
    // Load initial data
    loadPadInfo();
    
    // Set up event source for real-time updates - only for device events
    const eventSource = new EventSource('/events');
    
    eventSource.addEventListener('init', function(event) {
        const data = JSON.parse(event.data);
        if (data.devicePads !== undefined) {
            // Always set total pads to match the actual current count
            padStats.total = data.devicePads + padStats.used;
            savePadStats();
            updatePadCountUI(data.devicePads);
            updatePadStatsUI();
        }
    });
    
    eventSource.addEventListener('message', function(event) {
        const data = JSON.parse(event.data);
        if (data.devicePads !== undefined) {
            // Get the previous pad count before updating
            const previousPadCount = parseInt(padCountElement.textContent);
            
            // Update the UI with new pad count
            updatePadCountUI(data.devicePads);
            
            // Check if a pad was used
            if (!isNaN(previousPadCount) && data.devicePads < previousPadCount) {
                // Calculate how many pads were used
                const padsUsed = previousPadCount - data.devicePads;
                
                // Update used pads count
                padStats.used += padsUsed;
                
                // Total pads should remain the same since we're just transferring from available to used
                savePadStats();
                updatePadStatsUI();
                
                // If we have the pad size, immediately update the total space without waiting for API
                if (padStats.size > 0 && padFiles.length > 0) {
                    // Calculate new total space by removing the size of used pads
                    const usedSpace = padStats.size * padsUsed;
                    const totalSpace = getTotalPadSpace() - usedSpace;
                    
                    // Update the total space UI immediately
                    updateTotalSpaceUI(totalSpace);
                    
                    console.log(`Real-time update: ${padsUsed} pad(s) used, removed ${formatBytes(usedSpace)} from total space`);
                }
            }
            
            // Refresh pad files list after a short delay to let the server update
            setTimeout(() => {
                loadPadFiles();
            }, 500);
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
        loadPadInfo();
    });
    
    // Function to load pad info
    function loadPadInfo() {
        // Get device pad count
        loadPadCount();
        
        // Get pad files
        loadPadFiles();
        
        // Update the UI with pad stats
        updatePadStatsUI();
    }
    
    // Function to load pad count
    function loadPadCount() {
        fetch('/api/device/pads')
            .then(response => response.json())
            .then(data => {
                // Get current padCount from UI before updating
                const currentUiCount = parseInt(padCountElement.textContent);
                
                // Update the pad count UI
                updatePadCountUI(data.padCount);
                
                // If this is a fresh load (not a decrease from usage)
                // Update the total pads to be the current available + used
                padStats.total = data.padCount + padStats.used;
                savePadStats();
                updatePadStatsUI();
            })
            .catch(error => {
                console.error('Error fetching pad count:', error);
                padCountElement.textContent = 'Error';
            });
    }
    
    // Function to load actual pad files from the server
    function loadPadFiles() {
        fetch('/api/device/pad-files')
            .then(response => response.json())
            .then(data => {
                if (data.files && Array.isArray(data.files)) {
                    padFiles = data.files;
                    
                    // Calculate average pad size and total space
                    if (padFiles.length > 0) {
                        let totalSize = 0;
                        padFiles.forEach(file => {
                            totalSize += file.size;
                        });
                        padStats.size = Math.round(totalSize / padFiles.length);
                        
                        // Update the total space info in the UI
                        updateTotalSpaceUI(totalSize);
                        
                        // Update total pads to match the actual file count + used count
                        padStats.total = padFiles.length + padStats.used;
                        
                        savePadStats();
                        updatePadStatsUI();
                    } else {
                        updateTotalSpaceUI(0);
                        
                        // If no files, total should just be the used count
                        padStats.total = padStats.used;
                        savePadStats();
                        updatePadStatsUI();
                    }
                    
                    updateFileListUI();
                } else {
                    // If the API doesn't return files in the expected format
                    fileListElement.innerHTML = '';
                    const errorElement = document.createElement('div');
                    errorElement.className = 'no-files';
                    errorElement.textContent = 'Invalid file data received from server.';
                    fileListElement.appendChild(errorElement);
                    updateTotalSpaceUI(0);
                }
            })
            .catch(error => {
                console.error('Error fetching pad files:', error);
                // Show error in file list
                fileListElement.innerHTML = '';
                const errorElement = document.createElement('div');
                errorElement.className = 'no-files';
                errorElement.textContent = 'Error loading pad files. Please try again.';
                fileListElement.appendChild(errorElement);
                updateTotalSpaceUI(0);
            });
    }
    
    // Function to get the total space used by all pads
    function getTotalPadSpace() {
        if (!padFiles || padFiles.length === 0) {
            return 0;
        }
        
        let totalSize = 0;
        padFiles.forEach(file => {
            totalSize += file.size;
        });
        
        return totalSize;
    }
    
    // Function to update pad count UI
    function updatePadCountUI(count) {
        padCountElement.textContent = count;
    }
    
    // Function to update total space UI
    function updateTotalSpaceUI(totalBytes) {
        // Check if the total space element exists, if not create it
        let totalSpaceElement = document.getElementById('totalSpace');
        if (!totalSpaceElement) {
            // Create a new info card for total space
            const infoCard = document.createElement('div');
            infoCard.className = 'info-card';
            infoCard.innerHTML = `
                <div class="info-value" id="totalSpace">${formatBytes(totalBytes)}</div>
                <div class="info-label">Total Space</div>
            `;
            
            // Add it to the system info container
            const systemInfoContainer = document.querySelector('.system-info');
            systemInfoContainer.appendChild(infoCard);
        } else {
            // Just update the existing element
            totalSpaceElement.textContent = formatBytes(totalBytes);
        }
    }
    
    // Function to update file list UI
    function updateFileListUI() {
        fileListElement.innerHTML = '';
        
        if (padFiles.length === 0) {
            noFilesElement.style.display = 'block';
            fileListElement.appendChild(noFilesElement);
            return;
        }
        
        noFilesElement.style.display = 'none';
        
        // Add each file to the list
        padFiles.forEach(file => {
            const fileElement = document.createElement('div');
            fileElement.className = 'file-item';
            
            const formattedSize = formatBytes(file.size);
            const formattedDate = new Date(file.created).toLocaleString();
            
            fileElement.innerHTML = `
                <div class="file-name">${file.name}</div>
                <div class="file-size">${formattedSize} - ${formattedDate}</div>
            `;
            
            fileListElement.appendChild(fileElement);
        });
    }
    
    // Function to update pad stats UI
    function updatePadStatsUI() {
        totalPadsElement.textContent = padStats.total;
        usedPadsElement.textContent = padStats.used;
        padSizeElement.textContent = formatBytes(padStats.size);
    }
    
    // Function to save pad stats to localStorage
    function savePadStats() {
        localStorage.setItem('otp_device_pad_stats', JSON.stringify(padStats));
    }
    
    // Function to format bytes to a human-readable format
    function formatBytes(bytes, decimals = 2) {
        if (bytes === 0) return '0 Bytes';
        
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
        
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }
    
    // // Add reset button (DEBUG only - remove for production)
    // const container = document.querySelector('.system-info');
    // const resetButton = document.createElement('button');
    // resetButton.textContent = "Reset Stats";
    // resetButton.style.marginTop = "20px";
    // resetButton.style.padding = "5px 10px";
    // resetButton.style.backgroundColor = "#dc3545";
    // resetButton.style.color = "white";
    // resetButton.style.border = "none";
    // resetButton.style.borderRadius = "4px";
    // resetButton.style.cursor = "pointer";
    
    resetButton.addEventListener('click', function() {
        if (confirm('Reset all pad statistics? This will not affect actual pads.')) {
            // Reset statistics but keep pad size if we know it
            const padSize = padStats.size;
            padStats = {
                total: padFiles.length,
                used: 0,
                size: padSize
            };
            savePadStats();
            updatePadStatsUI();
            alert('Statistics reset.');
        }
    });
    
    document.body.appendChild(resetButton);
});