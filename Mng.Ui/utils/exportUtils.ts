/**
 * Export Utility Functions
 * CSV and JSON export functionality for lists and detail pages
 */

/**
 * Download a file to the user's computer
 * @param content - File content (string or Blob)
 * @param filename - File name
 * @param mimeType - MIME type (e.g., 'text/csv', 'application/json')
 */
export function downloadFile(content: string | Blob, filename: string, mimeType: string): void {
  const blob = content instanceof Blob ? content : new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Convert array of objects to CSV string
 * @param data - Array of objects to convert
 * @param headers - Optional custom headers (if not provided, uses object keys)
 * @returns CSV string
 */
export function arrayToCSV(data: any[], headers?: string[]): string {
  if (!data || data.length === 0) {
    return '';
  }

  // Get headers from first object if not provided
  const csvHeaders = headers || Object.keys(data[0]);
  
  // Escape CSV values (handle commas, quotes, newlines)
  const escapeCSV = (value: any): string => {
    if (value === null || value === undefined) {
      return '';
    }
    const stringValue = String(value);
    // If value contains comma, quote, or newline, wrap in quotes and escape quotes
    if (stringValue.includes(',') || stringValue.includes('"') || stringValue.includes('\n')) {
      return `"${stringValue.replace(/"/g, '""')}"`;
    }
    return stringValue;
  };

  // Create CSV rows
  const rows = [
    csvHeaders.map(escapeCSV).join(','), // Header row
    ...data.map(item => 
      csvHeaders.map(header => escapeCSV(item[header])).join(',')
    ),
  ];

  return rows.join('\n');
}

/**
 * Export array of objects to CSV file
 * @param data - Array of objects to export
 * @param filename - File name (without extension)
 * @param headers - Optional custom headers
 */
export function exportToCSV(data: any[], filename: string, headers?: string[]): void {
  const csvContent = arrayToCSV(data, headers);
  
  // Add UTF-8 BOM for Excel compatibility
  const BOM = '\uFEFF';
  const csvWithBOM = BOM + csvContent;
  
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
  const fullFilename = `${filename}_${timestamp}.csv`;
  
  downloadFile(csvWithBOM, fullFilename, 'text/csv;charset=utf-8;');
}

/**
 * Export array of objects to JSON file
 * @param data - Array of objects to export
 * @param filename - File name (without extension)
 */
export function exportArrayToJSON(data: any[], filename: string): void {
  const jsonContent = JSON.stringify(data, null, 2); // Pretty print with 2 spaces indent
  
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
  const fullFilename = `${filename}_${timestamp}.json`;
  
  downloadFile(jsonContent, fullFilename, 'application/json;charset=utf-8;');
}

/**
 * Export single object to JSON file
 * @param data - Object to export
 * @param filename - File name (without extension)
 */
export function exportObjectToJSON(data: any, filename: string): void {
  const jsonContent = JSON.stringify(data, null, 2); // Pretty print with 2 spaces indent
  
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
  const fullFilename = `${filename}_${timestamp}.json`;
  
  downloadFile(jsonContent, fullFilename, 'application/json;charset=utf-8;');
}
