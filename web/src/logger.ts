// ─── In-Memory Web Logger ────────────────────────────────────────────────────

export type LogLevel = "info" | "warn" | "error";

export interface LogEntry {
  timestamp: string;
  level: LogLevel;
  category: string;
  message: string;
}

const MAX_ENTRIES = 1000;
const entries: LogEntry[] = [];

export function log(level: LogLevel, category: string, message: string) {
  const entry: LogEntry = {
    timestamp: new Date().toISOString(),
    level,
    category,
    message,
  };
  entries.push(entry);
  if (entries.length > MAX_ENTRIES) {
    entries.splice(0, entries.length - MAX_ENTRIES);
  }
}

export function info(category: string, message: string) { log("info", category, message); }
export function warn(category: string, message: string) { log("warn", category, message); }
export function error(category: string, message: string) { log("error", category, message); }

export function getWebLogs(opts?: { tail?: number; level?: LogLevel; category?: string }): LogEntry[] {
  let result = entries;
  if (opts?.level) result = result.filter(e => e.level === opts.level);
  if (opts?.category) result = result.filter(e => e.category === opts.category);
  const tail = opts?.tail ?? 200;
  return result.slice(-tail);
}

export function getCategories(): string[] {
  return [...new Set(entries.map(e => e.category))].sort();
}
