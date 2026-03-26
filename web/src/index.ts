import { Elysia } from "elysia";
import { validateCredentials, generateSession, validateSession, destroySession } from "./auth";
import { getServerStatus, getServerStats, restartServer, stopServer, startServer } from "./docker";
import { RconClient } from "./rcon";
import { loginPage } from "./views/login";
import { dashboardPage } from "./views/dashboard";
import { rconPage } from "./views/rcon";

const PORT = parseInt(process.env.WEB_PORT || "3000");

const rcon = new RconClient(
  process.env.RUST_CONTAINER_NAME || "rust-server",
  parseInt(process.env.RUST_RCON_PORT || "28016"),
  process.env.RUST_RCON_PASSWORD || "changeme"
);

function getCookie(headers: Headers, name: string): string | undefined {
  const cookies = headers.get("cookie") || "";
  const match = cookies.match(new RegExp(`(?:^|;\\s*)${name}=([^;]*)`));
  return match?.[1];
}

function authGuard(headers: Headers): Response | null {
  const token = getCookie(headers, "session");
  if (!validateSession(token)) {
    return new Response(null, {
      status: 302,
      headers: { Location: "/login" },
    });
  }
  return null;
}

const app = new Elysia()
  // Login page
  .get("/login", () => new Response(loginPage(), { headers: { "Content-Type": "text/html" } }))

  .post("/login", async ({ body, set }) => {
    const { username, password } = body as { username: string; password: string };
    if (!validateCredentials(username, password)) {
      return new Response(loginPage("Invalid credentials"), {
        status: 401,
        headers: { "Content-Type": "text/html" },
      });
    }
    const token = generateSession();
    return new Response(null, {
      status: 302,
      headers: {
        Location: "/dashboard",
        "Set-Cookie": `session=${token}; Path=/; HttpOnly; SameSite=Strict; Max-Age=86400`,
      },
    });
  })

  .post("/logout", ({ headers }) => {
    const token = getCookie(headers, "session");
    if (token) destroySession(token);
    return new Response(null, {
      status: 302,
      headers: {
        Location: "/login",
        "Set-Cookie": "session=; Path=/; HttpOnly; Max-Age=0",
      },
    });
  })

  // Redirect root to dashboard
  .get("/", ({ headers }) => {
    const redirect = authGuard(headers) ? "/login" : "/dashboard";
    return new Response(null, { status: 302, headers: { Location: redirect } });
  })

  // Dashboard
  .get("/dashboard", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    const [status, stats] = await Promise.all([getServerStatus(), getServerStats()]);

    let serverInfo = { hostname: "", players: "", maxPlayers: "", map: "", fps: "" };
    if (status.running) {
      try {
        const raw = await rcon.command("serverinfo");
        const lines = raw.split("\n");
        for (const line of lines) {
          const [key, ...val] = line.split(":");
          const k = key?.trim().toLowerCase() || "";
          const v = val.join(":").trim();
          if (k === "hostname") serverInfo.hostname = v;
          if (k === "players") serverInfo.players = v.split(" ")[0] || v;
          if (k === "maxplayers") serverInfo.maxPlayers = v;
          if (k === "map") serverInfo.map = v;
          if (k === "fps") serverInfo.fps = v;
        }
      } catch {}
    }

    return new Response(dashboardPage({ status, stats, serverInfo }), {
      headers: { "Content-Type": "text/html" },
    });
  })

  // RCON page
  .get("/rcon", ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    return new Response(rconPage(), { headers: { "Content-Type": "text/html" } });
  })

  // API: RCON command
  .post("/api/rcon", async ({ headers, body }) => {
    const blocked = authGuard(headers);
    if (blocked) return { error: "unauthorized" };

    const { command } = body as { command: string };
    if (!command) return { error: "no command provided" };

    try {
      const response = await rcon.command(command);
      return { response };
    } catch (e: any) {
      return { error: e.message };
    }
  })

  // API: Server controls
  .post("/api/server/restart", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    await restartServer();
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/server/stop", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    await stopServer();
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/server/start", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    await startServer();
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .listen(PORT);

console.log(`Admin dashboard running at http://localhost:${PORT}`);
