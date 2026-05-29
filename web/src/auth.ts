const ADMIN_USER = process.env.ADMIN_USER || "admin";
const ADMIN_PASS = process.env.ADMIN_PASS || "changeme";

// Static bearer token for programmatic, browser-less access (e.g. piping prod logs into
// Claude Code from the CLI). Empty = disabled: no token will ever validate.
const API_TOKEN = process.env.LOGS_API_TOKEN || "";

const sessions = new Map<string, number>();

// Constant-time string compare to avoid leaking the token via response timing.
function timingSafeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let mismatch = 0;
  for (let i = 0; i < a.length; i++) mismatch |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return mismatch === 0;
}

export function isApiTokenConfigured(): boolean {
  return API_TOKEN.length > 0;
}

export function validateApiToken(token: string | undefined): boolean {
  if (!API_TOKEN || !token) return false;
  return timingSafeEqual(token, API_TOKEN);
}

export function generateSession(): string {
  const token = crypto.randomUUID();
  sessions.set(token, Date.now());
  return token;
}

export function validateSession(token: string | undefined): boolean {
  if (!token) return false;
  const created = sessions.get(token);
  if (!created) return false;
  // 24h expiry
  if (Date.now() - created > 86400000) {
    sessions.delete(token);
    return false;
  }
  return true;
}

export function validateCredentials(username: string, password: string): boolean {
  return username === ADMIN_USER && password === ADMIN_PASS;
}

export function destroySession(token: string) {
  sessions.delete(token);
}
