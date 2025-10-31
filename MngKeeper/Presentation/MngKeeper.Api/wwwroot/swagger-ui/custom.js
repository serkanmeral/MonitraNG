// Custom Swagger UI JavaScript
(function() {
    'use strict';

    // Wait for Swagger UI to load
    function waitForSwaggerUI() {
        if (typeof SwaggerUIBundle !== 'undefined') {
            initializeCustomFeatures();
        } else {
            setTimeout(waitForSwaggerUI, 100);
        }
    }

    function initializeCustomFeatures() {
        // Add custom header with API version
        addCustomHeader();
        
        // Add copy button to code blocks
        addCopyButtons();
        
        // Add search functionality
        addSearchFunctionality();
        
        // Add response time tracking
        addResponseTimeTracking();
        
        // Add dark mode toggle
        addDarkModeToggle();
        
        // Add export functionality
        addExportFunctionality();
    }

    function addCustomHeader() {
        const header = document.createElement('div');
        header.className = 'custom-header';
        header.innerHTML = `
            <div style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; margin-bottom: 20px;">
                <h2 style="margin: 0; font-size: 24px;">🚀 MngKeeper API Documentation</h2>
                <p style="margin: 10px 0 0 0; opacity: 0.9;">Multi-tenant Management System API v1.0</p>
                <div style="margin-top: 15px;">
                    <span style="background: rgba(255,255,255,0.2); padding: 5px 10px; border-radius: 15px; font-size: 12px; margin: 0 5px;">
                        🔐 JWT Authentication
                    </span>
                    <span style="background: rgba(255,255,255,0.2); padding: 5px 10px; border-radius: 15px; font-size: 12px; margin: 0 5px;">
                        📊 GraphQL Support
                    </span>
                    <span style="background: rgba(255,255,255,0.2); padding: 5px 10px; border-radius: 15px; font-size: 12px; margin: 0 5px;">
                        🔄 Real-time Events
                    </span>
                </div>
            </div>
        `;
        
        const swaggerContainer = document.querySelector('.swagger-ui');
        if (swaggerContainer) {
            swaggerContainer.insertBefore(header, swaggerContainer.firstChild);
        }
    }

    function addCopyButtons() {
        // Add copy button to all code blocks
        const codeBlocks = document.querySelectorAll('.highlight-code');
        codeBlocks.forEach(block => {
            if (!block.querySelector('.copy-button')) {
                const copyButton = document.createElement('button');
                copyButton.className = 'copy-button';
                copyButton.innerHTML = '📋 Copy';
                copyButton.style.cssText = `
                    position: absolute;
                    top: 5px;
                    right: 5px;
                    background: #3498db;
                    color: white;
                    border: none;
                    padding: 5px 10px;
                    border-radius: 3px;
                    cursor: pointer;
                    font-size: 12px;
                `;
                
                copyButton.addEventListener('click', function() {
                    const text = block.textContent;
                    navigator.clipboard.writeText(text).then(() => {
                        copyButton.innerHTML = '✅ Copied!';
                        setTimeout(() => {
                            copyButton.innerHTML = '📋 Copy';
                        }, 2000);
                    });
                });
                
                block.style.position = 'relative';
                block.appendChild(copyButton);
            }
        });
    }

    function addSearchFunctionality() {
        const searchContainer = document.createElement('div');
        searchContainer.className = 'search-container';
        searchContainer.innerHTML = `
            <div style="margin: 20px 0; padding: 15px; background: #f8f9fa; border-radius: 5px;">
                <input type="text" id="api-search" placeholder="🔍 Search endpoints..." 
                       style="width: 100%; padding: 10px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px;">
            </div>
        `;
        
        const swaggerContainer = document.querySelector('.swagger-ui');
        if (swaggerContainer) {
            const infoSection = swaggerContainer.querySelector('.info');
            if (infoSection) {
                infoSection.parentNode.insertBefore(searchContainer, infoSection.nextSibling);
            }
        }
        
        // Add search functionality
        const searchInput = document.getElementById('api-search');
        if (searchInput) {
            searchInput.addEventListener('input', function(e) {
                const searchTerm = e.target.value.toLowerCase();
                const opblocks = document.querySelectorAll('.opblock');
                
                opblocks.forEach(block => {
                    const summary = block.querySelector('.opblock-summary-description');
                    const path = block.querySelector('.opblock-summary-path');
                    
                    if (summary && path) {
                        const text = (summary.textContent + ' ' + path.textContent).toLowerCase();
                        if (text.includes(searchTerm)) {
                            block.style.display = 'block';
                        } else {
                            block.style.display = 'none';
                        }
                    }
                });
            });
        }
    }

    function addResponseTimeTracking() {
        // Track response times for API calls
        const originalFetch = window.fetch;
        window.fetch = function(...args) {
            const startTime = performance.now();
            return originalFetch.apply(this, args).then(response => {
                const endTime = performance.now();
                const duration = Math.round(endTime - startTime);
                
                // Add response time to console
                console.log(`API Response Time: ${duration}ms`);
                
                return response;
            });
        };
    }

    function addDarkModeToggle() {
        const toggleButton = document.createElement('button');
        toggleButton.innerHTML = '🌙';
        toggleButton.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: #2c3e50;
            color: white;
            border: none;
            padding: 10px;
            border-radius: 50%;
            cursor: pointer;
            font-size: 18px;
            z-index: 1000;
            box-shadow: 0 2px 10px rgba(0,0,0,0.2);
        `;
        
        document.body.appendChild(toggleButton);
        
        let isDarkMode = false;
        toggleButton.addEventListener('click', function() {
            isDarkMode = !isDarkMode;
            toggleButton.innerHTML = isDarkMode ? '☀️' : '🌙';
            
            if (isDarkMode) {
                document.body.style.filter = 'invert(90%) hue-rotate(180deg)';
            } else {
                document.body.style.filter = 'none';
            }
        });
    }

    function addExportFunctionality() {
        const exportButton = document.createElement('button');
        exportButton.innerHTML = '📄 Export API Spec';
        exportButton.style.cssText = `
            position: fixed;
            top: 70px;
            right: 20px;
            background: #27ae60;
            color: white;
            border: none;
            padding: 10px 15px;
            border-radius: 5px;
            cursor: pointer;
            font-size: 14px;
            z-index: 1000;
            box-shadow: 0 2px 10px rgba(0,0,0,0.2);
        `;
        
        document.body.appendChild(exportButton);
        
        exportButton.addEventListener('click', function() {
            // Export OpenAPI specification
            fetch('/api-docs/v1/swagger.json')
                .then(response => response.json())
                .then(data => {
                    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = 'mngkeeper-api-spec.json';
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    URL.revokeObjectURL(url);
                });
        });
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', waitForSwaggerUI);
    } else {
        waitForSwaggerUI();
    }

    // Re-initialize when Swagger UI content changes
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.type === 'childList') {
                setTimeout(initializeCustomFeatures, 500);
            }
        });
    });

    // Start observing
    setTimeout(() => {
        const swaggerContainer = document.querySelector('.swagger-ui');
        if (swaggerContainer) {
            observer.observe(swaggerContainer, { childList: true, subtree: true });
        }
    }, 1000);

})();
