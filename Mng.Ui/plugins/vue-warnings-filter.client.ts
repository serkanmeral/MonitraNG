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

  // Not: global console.warn override kaldırıldı — yanlışlıkla uygulama loglarını yutmaması için.
  // Sadece Vue warnHandler ile filtre uygulanır.

  nuxtApp.hook('app:beforeUnmount', () => {
    if (originalWarnHandler) {
      nuxtApp.vueApp.config.warnHandler = originalWarnHandler;
    }
  });
});
