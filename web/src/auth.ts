const ADMIN_USER = process.env.ADMIN_USER || "admin";
const ADMIN_PASS = process.env.ADMIN_PASS || "changeme";

const sessions = new Map<string, number>();

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
