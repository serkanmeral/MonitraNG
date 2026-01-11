// Simple Export Script - Direct TypeScript Parsing
// This script manually parses the sidebarItem.ts file and converts it to MongoDB format

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const projectRoot = path.resolve(__dirname, '../../../../');
const sidebarItemPath = path.join(projectRoot, 'Mng.Ui/components/lc/Full/vertical-sidebar/sidebarItem.ts');
const outputPath = path.join(__dirname, 'menu-items-export.json');

console.log(`📂 Reading: ${sidebarItemPath}`);
console.log('');

// Read file
const content = fs.readFileSync(sidebarItemPath, 'utf-8');

// Extract icon imports
const iconNames = [];
const iconImportMatch = content.match(/import\s*\{([^}]+)\}\s*from\s*["']vue-tabler-icons["']/);
if (iconImportMatch) {
    iconImportMatch[1].split(',').forEach(name => {
        iconNames.push(name.trim());
    });
}
console.log(`✅ Found ${iconNames.length} icon imports`);

// Manual parsing: Convert to simplified format
// We'll extract each menu object manually
const menuItems = [];
let order = 0;
let currentLevel = 0;
let currentParentId = null;
const parentStack = [];

// Split by top-level objects (headers and items)
// Pattern: { header: "..." } or { title: "..." }
const lines = content.split('\n');
let currentObject = null;
let inObject = false;
let objectDepth = 0;
let objectLines = [];

function processObject(objText) {
    const obj = {};
    
    // Extract header
    const headerMatch = objText.match(/header\s*:\s*["']([^"']+)["']/);
    if (headerMatch) {
        obj.itemType = 'header';
        obj.header = headerMatch[1];
        obj.order = order++;
        obj.level = 0;
        obj.parentId = null;
        currentLevel = 0;
        currentParentId = null;
        parentStack.length = 0;
        menuItems.push(obj);
        return;
    }
    
    // Extract title
    const titleMatch = objText.match(/title\s*:\s*["']([^"']+)["']/);
    if (titleMatch) {
        obj.itemType = 'item';
        obj.title = titleMatch[1];
        
        // Extract icon
        const iconMatch = objText.match(/icon\s*:\s*(\w+Icon)/);
        if (iconMatch) {
            obj.icon = iconMatch[1];
            obj.iconType = 'tabler';
        }
        
        // Extract to
        const toMatch = objText.match(/to\s*:\s*["']([^"']+)["']/);
        if (toMatch) {
            obj.to = toMatch[1];
            obj.type = 'internal';
        }
        
        // Extract chip
        const chipMatch = objText.match(/chip\s*:\s*["']([^"']+)["']/);
        if (chipMatch) {
            obj.chip = chipMatch[1];
        }
        
        // Extract chipColor
        const chipColorMatch = objText.match(/chipColor\s*:\s*["']([^"']+)["']/);
        if (chipColorMatch) {
            obj.chipColor = chipColorMatch[1];
        }
        
        // Extract chipBgColor
        const chipBgColorMatch = objText.match(/chipBgColor\s*:\s*["']([^"']+)["']/);
        if (chipBgColorMatch) {
            obj.chipBgColor = chipBgColorMatch[1];
        }
        
        // Extract chipVariant
        const chipVariantMatch = objText.match(/chipVariant\s*:\s*["']([^"']+)["']/);
        if (chipVariantMatch) {
            obj.chipVariant = chipVariantMatch[1];
        }
        
        // Extract chipIcon
        const chipIconMatch = objText.match(/chipIcon\s*:\s*["']([^"']+)["']/);
        if (chipIconMatch) {
            obj.chipIcon = chipIconMatch[1];
        }
        
        // Extract disabled
        const disabledMatch = objText.match(/disabled\s*:\s*(true|false)/);
        if (disabledMatch) {
            obj.disabled = disabledMatch[1] === 'true';
        }
        
        // Extract subCaption
        const subCaptionMatch = objText.match(/subCaption\s*:\s*["']([^"']+)["']/);
        if (subCaptionMatch) {
            obj.subCaption = subCaptionMatch[1];
        }
        
        obj.order = order++;
        obj.level = currentLevel;
        obj.parentId = currentParentId;
        obj.pageType = 'user';
        
        // Check if has children
        if (objText.includes('children:')) {
            // This will be handled separately - for now mark as parent
            // Children processing will need recursive parsing
        }
        
        menuItems.push(obj);
        
        // If this item has children, next items will be one level deeper
        if (objText.includes('children:')) {
            currentLevel++;
            parentStack.push(obj.order - 1); // Store order as temp parent ID
            currentParentId = obj.order - 1;
        }
        
        return;
    }
}

// Simple line-by-line parsing
for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();
    
    // Skip empty lines and comments
    if (!line || line.startsWith('//')) {
        continue;
    }
    
    // Check for object start
    if (line.match(/^\{\s*(header|title)\s*:/)) {
        if (currentObject) {
            // Process previous object
            processObject(currentObject.join('\n'));
        }
        currentObject = [line];
        inObject = true;
        objectDepth = 1;
        continue;
    }
    
    if (inObject) {
        currentObject.push(lines[i]);
        
        // Count brackets
        const openBraces = (line.match(/\{/g) || []).length;
        const closeBraces = (line.match(/\}/g) || []).length;
        objectDepth += openBraces - closeBraces;
        
        // Check for object end
        if (objectDepth === 0) {
            // Process object
            processObject(currentObject.join('\n'));
            currentObject = null;
            inObject = false;
        }
        
        // Check if children array ended (simplified)
        if (line.includes(']') && currentParentId !== null) {
            // Check if we're closing a children array
            // This is a simplified check - might need refinement
            const childrenEndMatch = line.match(/^\s*\]\s*,?\s*$/);
            if (childrenEndMatch) {
                currentLevel = Math.max(0, currentLevel - 1);
                if (parentStack.length > 0) {
                    parentStack.pop();
                    currentParentId = parentStack.length > 0 ? parentStack[parentStack.length - 1] : null;
                } else {
                    currentParentId = null;
                }
            }
        }
    }
}

// Process last object if exists
if (currentObject) {
    processObject(currentObject.join('\n'));
}

console.log(`✅ Parsed ${menuItems.length} menu items`);
console.log('');

// Fix parentId references - use order-based temporary IDs, will be converted to actual __dataId after insert
// For now, we'll use null and let the load script handle hierarchy
// Or we can use a simple approach: track parent by order

// Second pass: Fix parentId references based on hierarchy
const fixedItems = [];
const itemOrderMap = new Map(); // order -> index mapping

menuItems.forEach((item, index) => {
    itemOrderMap.set(item.order, index);
});

menuItems.forEach((item) => {
    // If parentId is a number (order), we need to keep it for now
    // The load script will handle converting to actual __dataId
    // For headers, parentId is always null
    if (item.itemType === 'header') {
        item.parentId = null;
        item.level = 0;
    }
    
    // For items with parentId, keep it as order reference for now
    // Will be converted in load script
    
    fixedItems.push(item);
});

// Save to JSON
fs.writeFileSync(outputPath, JSON.stringify(fixedItems, null, 2), 'utf-8');

console.log(`✅ Exported to: ${outputPath}`);
console.log(`📊 Total items: ${fixedItems.length}`);
console.log('');

const headerCount = fixedItems.filter(item => item.itemType === 'header').length;
const itemCount = fixedItems.filter(item => item.itemType === 'item').length;

console.log('📈 Summary:');
console.log(`   Headers: ${headerCount}`);
console.log(`   Items: ${itemCount}`);
console.log('');
console.log('✅ Export completed!');
console.log('');
console.log('⚠️  Note: parentId references use temporary order-based IDs.');
console.log('   The load script will need to handle hierarchy properly.');
console.log('');
