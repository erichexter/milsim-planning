import { useState, useEffect } from 'react';

export type Theme = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'rp0-theme';

function resolveTheme(theme: Theme): 'light' | 'dark' {
  if (theme === 'system') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  return theme;
}

function applyTheme(resolved: 'light' | 'dark') {
  document.documentElement.classList.toggle('dark', resolved === 'dark');
}

export function useTheme() {
  const [theme, setThemeState] = useState<Theme>(() => {
    const stored = localStorage.getItem(STORAGE_KEY) as Theme | null;
    return stored ?? 'system';
  });

  // Apply on mount and whenever theme changes
  useEffect(() => {
    const resolved = resolveTheme(theme);
    applyTheme(resolved);

    // When using system, watch for OS changes
    if (theme === 'system') {
      const mq = window.matchMedia('(prefers-color-scheme: dark)');
      const handler = (e: MediaQueryListEvent) => applyTheme(e.matches ? 'dark' : 'light');
      mq.addEventListener('change', handler);
      return () => mq.removeEventListener('change', handler);
    }
  }, [theme]);

  function setTheme(next: Theme) {
    if (next === 'system') {
      localStorage.removeItem(STORAGE_KEY);
    } else {
      localStorage.setItem(STORAGE_KEY, next);
    }
    setThemeState(next);
  }

  // Derive the effective resolved value for the toggle icon
  const isDark = resolveTheme(theme) === 'dark';

  return { theme, setTheme, isDark };
}
