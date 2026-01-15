/**
 * Vue Warnings Filter Plugin
 * 
 * This plugin filters out known non-critical Vue warnings that are generated
 * by Vuetify components (especially VMenu, VOverlay, VDefaultsProvider).
 * 
 * These warnings are typically:
 * - "Slot 'default' invoked outside of the render function"
 * - These are internal Vuetify implementation details and don't affect functionality
 */

export default defineNuxtPlugin((nuxtApp) => {
  // Only run on client side
  if (process.server) {
    return;
  }

  // Use Vue's warnHandler to filter warnings at the source
  const originalWarnHandler = nuxtApp.vueApp.config.warnHandler;
  
  nuxtApp.vueApp.config.warnHandler = (msg, instance, trace) => {
    // Check if this is a slot warning from Vuetify components
    const message = msg?.toString() || '';
    const traceStr = trace?.toString() || '';
    
    // Filter out slot warnings that mention Vuetify components
    if (
      message.includes('Slot "default" invoked outside of the render function') &&
      (
        traceStr.includes('VMenu') ||
        traceStr.includes('VOverlay') ||
        traceStr.includes('VDefaultsProvider') ||
        traceStr.includes('BaseTransition') ||
        traceStr.includes('VDialogTransition') ||
        traceStr.includes('VListItem') ||
        traceStr.includes('VAvatar') ||
        traceStr.includes('VBtn') ||
        traceStr.includes('ProfileDD')
      )
    ) {
      // Suppress this warning - it's a known Vuetify internal issue
      return;
    }

    // For all other warnings, call the original handler or default behavior
    if (originalWarnHandler) {
      originalWarnHandler(msg, instance, trace);
    } else {
      // Fallback to default Vue warning behavior
      console.warn(`[Vue warn]: ${msg}`);
      if (trace) {
        console.warn(trace);
      }
    }
  };

  // Also filter console.warn as a backup (in case Vue doesn't use warnHandler)
  const originalConsoleWarn = console.warn;
  console.warn = (...args: any[]) => {
    // Check all arguments for slot warning patterns
    const allArgs = args.map(arg => arg?.toString() || '').join(' ');
    
    if (
      allArgs.includes('Slot "default" invoked outside of the render function') &&
      (
        allArgs.includes('VMenu') ||
        allArgs.includes('VOverlay') ||
        allArgs.includes('VDefaultsProvider') ||
        allArgs.includes('BaseTransition') ||
        allArgs.includes('VDialogTransition') ||
        allArgs.includes('VListItem') ||
        allArgs.includes('VAvatar') ||
        allArgs.includes('VBtn') ||
        allArgs.includes('ProfileDD')
      )
    ) {
      // Suppress this warning
      return;
    }

    // For all other warnings, call the original console.warn
    originalConsoleWarn.apply(console, args);
  };

  // Cleanup on app unmount (optional, but good practice)
  nuxtApp.hook('app:beforeUnmount', () => {
    // Restore original handlers
    if (originalWarnHandler) {
      nuxtApp.vueApp.config.warnHandler = originalWarnHandler;
    }
    console.warn = originalConsoleWarn;
  });
});
