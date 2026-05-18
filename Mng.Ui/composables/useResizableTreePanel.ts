import { ref, onMounted, onUnmounted } from 'vue';

export interface ResizableTreePanelOptions {
  minWidth?: number;
  maxWidth?: number;
  defaultWidth?: number;
}

/**
 * Monitoring Kontrol sayfasındaki sol panel deseni: genişlik + collapse, localStorage.
 */
export function useResizableTreePanel(storageKey: string, options: ResizableTreePanelOptions = {}) {
  const min = options.minWidth ?? 200;
  const max = options.maxWidth ?? 480;
  const defW = options.defaultWidth ?? 320;

  function loadState(): { width: number; collapsed: boolean } {
    if (typeof window === 'undefined') return { width: defW, collapsed: false };
    try {
      const raw = localStorage.getItem(storageKey);
      if (raw) {
        const o = JSON.parse(raw) as { width?: number; collapsed?: boolean };
        return {
          width: Math.min(max, Math.max(min, o.width ?? defW)),
          collapsed: !!o.collapsed,
        };
      }
    } catch (_) {}
    return { width: defW, collapsed: false };
  }

  function saveState(width: number, collapsed: boolean) {
    try {
      localStorage.setItem(storageKey, JSON.stringify({ width, collapsed }));
    } catch (_) {}
  }

  const initial = loadState();
  const treeWidth = ref(initial.width);
  const treeCollapsed = ref(initial.collapsed);
  const resizeActive = ref(false);

  function startResize() {
    resizeActive.value = true;
  }

  function onResizeMove(e: MouseEvent) {
    if (!resizeActive.value) return;
    const w = Math.min(max, Math.max(min, e.clientX));
    treeWidth.value = w;
    saveState(w, treeCollapsed.value);
  }

  function stopResize() {
    resizeActive.value = false;
  }

  function toggleTreeCollapse() {
    treeCollapsed.value = !treeCollapsed.value;
    saveState(treeWidth.value, treeCollapsed.value);
  }

  onMounted(() => {
    window.addEventListener('mousemove', onResizeMove);
    window.addEventListener('mouseup', stopResize);
  });
  onUnmounted(() => {
    window.removeEventListener('mousemove', onResizeMove);
    window.removeEventListener('mouseup', stopResize);
  });

  return {
    treeWidth,
    treeCollapsed,
    resizeActive,
    startResize,
    toggleTreeCollapse,
  };
}
