import { Elysia } from "elysia";
import { validateCredentials, generateSession, validateSession, destroySession } from "./auth";
import { getServerStatus, getServerStats, getServerLogs, restartServer, stopServer, startServer, execInServer } from "./docker";
import { RconClient } from "./rcon";
import { loginPage } from "./views/login";
import { dashboardPage } from "./views/dashboard";
import { rconPage } from "./views/rcon";
import { logsPage } from "./views/logs";
import { configPage } from "./views/config";
import { configsListPage, configsEditPage } from "./views/configs";
import { npcsPage } from "./views/npcs";
import { stackSizePage } from "./views/stacksize";
import { settingsPage, type ServerSettings } from "./views/settings";
import { readdirSync, readFileSync, writeFileSync, unlinkSync, existsSync } from "fs";
import { join, basename } from "path";

const PORT = parseInt(process.env.WEB_PORT || "3000");

const rcon = new RconClient(
  process.env.RUST_CONTAINER_NAME || "rust-server",
  parseInt(process.env.RUST_RCON_PORT || "28016"),
  process.env.RUST_RCON_PASSWORD || "changeme"
);

function getCookie(headers: Record<string, string | undefined>, name: string): string | undefined {
  const cookies = headers.cookie || "";
  const match = cookies.match(new RegExp(`(?:^|;\\s*)${name}=([^;]*)`));
  return match?.[1];
}

function authGuard(headers: Record<string, string | undefined>): Response | null {
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
        const info = JSON.parse(raw);
        serverInfo.hostname = String(info.Hostname ?? "");
        serverInfo.players = String(info.Players ?? "0");
        serverInfo.maxPlayers = String(info.MaxPlayers ?? "0");
        serverInfo.map = String(info.Map ?? "");
        serverInfo.fps = String(info.Framerate ?? info.Fps ?? "");
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

  // NPC Manager page
  .get("/npcs", ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    return new Response(npcsPage(), { headers: { "Content-Type": "text/html" } });
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

  // Logs page
  .get("/logs", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    const logs = await getServerLogs(200);
    return new Response(logsPage(logs), { headers: { "Content-Type": "text/html" } });
  })

  // API: Logs
  .get("/api/logs", async ({ headers, query }) => {
    const blocked = authGuard(headers);
    if (blocked) return { error: "unauthorized" };
    const tail = Math.min(parseInt(query.tail || "200") || 200, 5000);
    const logs = await getServerLogs(tail);
    return { logs };
  })

  // Config page
  .get("/config/gungame", async ({ headers, query }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    let config: Record<string, any> | null = null;
    let error: string | undefined;
    let success: string | undefined;

    if (query.saved === "1") success = "Config saved and plugin reloaded.";

    try {
      const configPath = "/rust-data/oxide/config/GunGame.json";
      const file = Bun.file(configPath);
      const raw = await file.text();
      config = JSON.parse(raw);
    } catch (e: any) {
      error = "Failed to load config: " + e.message;
    }

    return new Response(configPage({ config, error, success }), {
      headers: { "Content-Type": "text/html" },
    });
  })

  // API: Save config
  .post("/api/config/gungame/save", async ({ headers, body }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    const configPath = "/rust-data/oxide/config/GunGame.json";

    try {
      // Read current config
      const file = Bun.file(configPath);
      const raw = await file.text();
      const config = JSON.parse(raw);

      const form = body as Record<string, string>;

      // Apply simple fields
      const intFields = ["XPPerKill", "HeadshotBonusXP", "DistanceBonusXPPer50m", "XPPerAnimalKill", "XPPerNPCKill", "MaxLevel", "TopListSize", "KillRewardMinAmount", "KillRewardMaxAmount"];
      for (const key of intFields) {
        if (key in form) config[key] = parseInt(form[key]) || 0;
      }

      const floatKey = "DifficultyMultiplier (scales XP earned: 0.5=hard, 1.0=normal, 2.0=easy)";
      if (floatKey in form) config[floatKey] = parseFloat(form[floatKey]) || 1.0;

      const strFields = ["KitPrefix", "ChatPrefix", "KillRewardItemShortname"];
      for (const key of strFields) {
        if (key in form) config[key] = form[key];
      }

      if ("WipeOnNewSave" in form) config.WipeOnNewSave = String(form.WipeOnNewSave).includes("true");
      config.TestMode = "TestMode" in form && String(form.TestMode).includes("true");

      // Rebuild thresholds based on MaxLevel
      const maxLevel = config.MaxLevel ?? 5;
      const newThresholds: Record<string, number> = {};
      for (let level = 2; level <= maxLevel; level++) {
        const formVal = form[`threshold_${level}`];
        if (formVal !== undefined) {
          newThresholds[String(level)] = parseInt(formVal as string) || 0;
        } else if (config.LevelXPThresholds?.[String(level)] !== undefined) {
          newThresholds[String(level)] = config.LevelXPThresholds[String(level)];
        }
      }
      config.LevelXPThresholds = newThresholds;

      // Write config back
      const json = JSON.stringify(config, null, 2);
      await Bun.write(configPath, json);

      // Reload plugin to pick up changes
      try { await rcon.command("oxide.reload GunGame"); } catch {}

      return new Response(null, { status: 302, headers: { Location: "/config/gungame?saved=1" } });
    } catch (e: any) {
      const configData = { config: null, error: "Failed to save config: " + e.message };
      return new Response(configPage(configData), { headers: { "Content-Type": "text/html" } });
    }
  })

  // StackSizeController config page
  .get("/config/stacksize", async ({ headers, query }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    let config: Record<string, any> | null = null;
    let error: string | undefined;
    let success: string | undefined;

    if (query.saved === "1") success = "Stack sizes saved and plugin reloaded.";

    try {
      const configPath = "/rust-data/oxide/config/StackSizeController.json";
      const file = Bun.file(configPath);
      const raw = await file.text();
      config = JSON.parse(raw);
    } catch (e: any) {
      error = "Failed to load config: " + e.message;
    }

    return new Response(stackSizePage({ config, error, success }), {
      headers: { "Content-Type": "text/html" },
    });
  })

  // API: Save StackSizeController config
  .post("/api/config/stacksize/save", async ({ headers, body }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    const configPath = "/rust-data/oxide/config/StackSizeController.json";

    try {
      const file = Bun.file(configPath);
      const raw = await file.text();
      const config = JSON.parse(raw);

      const form = body as Record<string, string>;

      // Global multiplier
      const globalKey = "Default Stack Multiplier (applies to all items not specifically configured)";
      if ("global_multiplier" in form) {
        const val = parseFloat(form.global_multiplier) || 1;
        // Try both known key formats
        if (globalKey in config) {
          config[globalKey] = val;
        } else if ("GlobalStackMultiplier" in config) {
          config["GlobalStackMultiplier"] = val;
        } else {
          config[globalKey] = val;
        }
      }

      // Category multipliers
      if (!config["Category Multipliers"]) config["Category Multipliers"] = {};
      for (const [key, value] of Object.entries(form)) {
        if (key.startsWith("cat_")) {
          const category = key.slice(4);
          if (value === "" || value === undefined) {
            delete config["Category Multipliers"][category];
          } else {
            config["Category Multipliers"][category] = parseFloat(value) || 0;
          }
        }
      }

      // Individual item multipliers - rebuild from form
      const items: Record<string, number> = {};
      for (const [key, value] of Object.entries(form)) {
        if (key.startsWith("item_")) {
          const itemName = key.slice(5);
          items[itemName] = parseFloat(value) || 1;
        }
      }
      config["Individual Item Multipliers"] = items;

      const json = JSON.stringify(config, null, 2);
      await Bun.write(configPath, json);

      try { await rcon.command("oxide.reload StackSizeController"); } catch {}

      return new Response(null, { status: 302, headers: { Location: "/config/stacksize?saved=1" } });
    } catch (e: any) {
      return new Response(stackSizePage({ config: null, error: "Failed to save config: " + e.message }), {
        headers: { "Content-Type": "text/html" },
      });
    }
  })

  // Server Settings
  .get("/server/settings", async ({ headers, query }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    let settings: ServerSettings;
    let error: string | undefined;
    let success: string | undefined;

    if (query.saved === "1") success = "Settings saved successfully.";

    const settingsPath = "/cfg/server-settings.json";
    try {
      const file = Bun.file(settingsPath);
      if (await file.exists()) {
        settings = JSON.parse(await file.text());
      } else {
        // Default from env vars on first visit
        settings = {
          serverName: process.env.RUST_SERVER_NAME || "Rust Server",
          serverIdentity: process.env.RUST_SERVER_IDENTITY || "docker",
          serverDescription: process.env.RUST_SERVER_DESCRIPTION || "",
          serverUrl: process.env.RUST_SERVER_URL || "",
          serverHeaderImage: process.env.RUST_SERVER_HEADERIMAGE || "",
          serverLogoImage: process.env.RUST_SERVER_LOGOIMAGE || "",
          serverTags: process.env.RUST_SERVER_TAGS || "",
          mapSeed: parseInt(process.env.RUST_SERVER_SEED || "12345"),
          worldSize: parseInt(process.env.RUST_SERVER_WORLDSIZE || "3500"),
          maxPlayers: parseInt(process.env.RUST_SERVER_MAXPLAYERS || "100"),
          saveInterval: 600,
          serverPort: parseInt(process.env.RUST_SERVER_PORT || "28015"),
          queryPort: parseInt(process.env.RUST_SERVER_QUERYPORT || "28017"),
          rconPort: parseInt(process.env.RUST_RCON_PORT || "28016"),
          appPort: parseInt(process.env.RUST_APP_PORT || "28082"),
          tickrate: 30,
          globalChat: true,
          stability: true,
          radiation: true,
          idleKick: 30,
          idleKickMode: "1",
          updateOnStart: process.env.RUST_UPDATE_ON_START !== "0",
          umodEnabled: process.env.UMOD_ENABLED !== "0",
          serverMode: "vanilla",
        };
      }
    } catch (e: any) {
      error = "Failed to load settings: " + e.message;
      settings = {
        serverName: "Rust Server", serverIdentity: "docker",
        serverDescription: "", serverUrl: "", serverHeaderImage: "", serverLogoImage: "", serverTags: "",
        mapSeed: 12345, worldSize: 3500, maxPlayers: 100, saveInterval: 600,
        serverPort: 28015, queryPort: 28017, rconPort: 28016, appPort: 28082,
        tickrate: 30, globalChat: true, stability: true, radiation: true,
        idleKick: 30, idleKickMode: "1",
        updateOnStart: true, umodEnabled: true, serverMode: "vanilla",
      };
    }

    return new Response(settingsPage({ settings, error, success }), {
      headers: { "Content-Type": "text/html" },
    });
  })

  .post("/api/server/settings/save", async ({ headers, body }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    try {
      const form = body as Record<string, string>;

      const settings: ServerSettings = {
        serverName: form.serverName || "Rust Server",
        serverIdentity: form.serverIdentity || "docker",
        serverDescription: form.serverDescription || "",
        serverUrl: form.serverUrl || "",
        serverHeaderImage: form.serverHeaderImage || "",
        serverLogoImage: form.serverLogoImage || "",
        serverTags: form.serverTags || "",
        mapSeed: parseInt(form.mapSeed) || 12345,
        worldSize: parseInt(form.worldSize) || 3500,
        maxPlayers: parseInt(form.maxPlayers) || 100,
        saveInterval: parseInt(form.saveInterval) || 600,
        serverPort: parseInt(form.serverPort) || 28015,
        queryPort: parseInt(form.queryPort) || 28017,
        rconPort: parseInt(form.rconPort) || 28016,
        appPort: parseInt(form.appPort) || 28082,
        tickrate: parseInt(form.tickrate) || 30,
        globalChat: String(form.globalChat).includes("true"),
        stability: String(form.stability).includes("true"),
        radiation: String(form.radiation).includes("true"),
        idleKick: parseInt(form.idleKick) || 30,
        idleKickMode: form.idleKickMode || "1",
        updateOnStart: String(form.updateOnStart).includes("true"),
        umodEnabled: String(form.umodEnabled).includes("true"),
        serverMode: form.serverMode || "vanilla",
      };

      // Write settings JSON
      await Bun.write("/cfg/server-settings.json", JSON.stringify(settings, null, 2));

      // Update server.cfg with mode-specific settings
      const cfgPath = "/cfg/server.cfg";
      let cfgContent = "";
      try { cfgContent = readFileSync(cfgPath, "utf-8"); } catch {}

      // Strip existing managed lines
      const managedKeys = [
        "server.pve", "server.gamemode", "craft.instant", "decay.scale",
        "server.description", "server.url", "server.headerimage", "server.logoimage", "server.tags",
        "server.saveinterval", "server.tickrate", "server.globalchat", "server.stability",
        "server.radiation", "server.idlekick", "server.idlekickmode",
      ];
      const lines = cfgContent.split("\n").filter(l => {
        const trimmed = l.trim().toLowerCase();
        return !managedKeys.some(k => trimmed.startsWith(k));
      });

      // Add branding lines
      const cfgLines: string[] = [];
      if (settings.serverDescription) cfgLines.push(`server.description "${settings.serverDescription}"`);
      if (settings.serverUrl) cfgLines.push(`server.url "${settings.serverUrl}"`);
      if (settings.serverHeaderImage) cfgLines.push(`server.headerimage "${settings.serverHeaderImage}"`);
      if (settings.serverLogoImage) cfgLines.push(`server.logoimage "${settings.serverLogoImage}"`);
      if (settings.serverTags) cfgLines.push(`server.tags "${settings.serverTags}"`);

      // Add gameplay lines
      cfgLines.push(`server.saveinterval ${settings.saveInterval}`);
      cfgLines.push(`server.tickrate ${settings.tickrate}`);
      cfgLines.push(`server.globalchat ${settings.globalChat}`);
      cfgLines.push(`server.stability ${settings.stability}`);
      cfgLines.push(`server.radiation ${settings.radiation}`);
      cfgLines.push(`server.idlekick ${settings.idleKick}`);
      cfgLines.push(`server.idlekickmode ${settings.idleKickMode}`);

      // Add mode-specific lines
      switch (settings.serverMode) {
        case "pve":
          cfgLines.push("server.pve true");
          break;
        case "softcore":
          cfgLines.push("server.pve false");
          cfgLines.push("server.gamemode softcore");
          break;
        case "creative":
          cfgLines.push("server.pve true");
          cfgLines.push("craft.instant true");
          cfgLines.push("decay.scale 0");
          break;
        case "gungame":
          cfgLines.push("server.pve false");
          break;
        default: // vanilla
          cfgLines.push("server.pve false");
          break;
      }

      const finalCfg = lines.filter(l => l.trim()).concat(cfgLines).join("\n") + "\n";
      writeFileSync(cfgPath, finalCfg, "utf-8");

      return new Response(null, { status: 302, headers: { Location: "/server/settings?saved=1" } });
    } catch (e: any) {
      return new Response(settingsPage({ settings: null, error: "Failed to save settings: " + e.message }), {
        headers: { "Content-Type": "text/html" },
      });
    }
  })

  // Server Configs - file browser & editor
  .get("/configs", ({ headers, query }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    const success = query.reloaded === "1" ? "Server configs reloaded." : undefined;
    try {
      const files = readdirSync("/cfg").filter(f => !f.startsWith(".")).sort();
      return new Response(configsListPage({ files, success }), { headers: { "Content-Type": "text/html" } });
    } catch (e: any) {
      return new Response(configsListPage({ files: [], error: "Failed to read /cfg: " + e.message }), {
        headers: { "Content-Type": "text/html" },
      });
    }
  })

  .get("/configs/:filename", ({ headers, params, query }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    const filename = basename(params.filename);
    const filepath = join("/cfg", filename);
    try {
      const content = readFileSync(filepath, "utf-8");
      const success = query.saved === "1" ? "File saved successfully." : undefined;
      return new Response(configsEditPage({ filename, content, success }), {
        headers: { "Content-Type": "text/html" },
      });
    } catch (e: any) {
      return new Response(configsEditPage({ filename, content: "", error: "Failed to read file: " + e.message }), {
        headers: { "Content-Type": "text/html" },
      });
    }
  })

  .post("/api/configs/reload", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await rcon.command("server.readcfg");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/configs?reloaded=1" } });
  })

  .post("/api/configs/create", async ({ headers, body }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    const form = body as Record<string, string>;
    const filename = basename(form.filename || "").trim();
    if (!filename || filename.startsWith(".") || filename.includes("/")) {
      return new Response(null, { status: 302, headers: { Location: "/configs" } });
    }

    const filepath = join("/cfg", filename);
    if (!existsSync(filepath)) {
      writeFileSync(filepath, "", "utf-8");
    }
    return new Response(null, { status: 302, headers: { Location: `/configs/${encodeURIComponent(filename)}` } });
  })

  .post("/api/configs/:filename/save", async ({ headers, body, params }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    const filename = basename(params.filename);
    const filepath = join("/cfg", filename);
    const form = body as Record<string, string>;

    try {
      writeFileSync(filepath, form.content || "", "utf-8");
      return new Response(null, { status: 302, headers: { Location: `/configs/${encodeURIComponent(filename)}?saved=1` } });
    } catch (e: any) {
      return new Response(configsEditPage({ filename, content: form.content || "", error: "Failed to save: " + e.message }), {
        headers: { "Content-Type": "text/html" },
      });
    }
  })

  .post("/api/configs/:filename/delete", async ({ headers, params }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;

    const filename = basename(params.filename);
    const filepath = join("/cfg", filename);
    try {
      unlinkSync(filepath);
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/configs" } });
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

  // API: Plugin controls
  .post("/api/plugins/reload-all", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      const response = await rcon.command("oxide.reload *");
      return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
    } catch {
      return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
    }
  })

  .post("/api/plugins/reload-gungame", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await rcon.command("oxide.reload GunGame");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/world/set-day", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await rcon.command("env.time 12");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/world/set-night", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await rcon.command("env.time 0");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/weather/clear", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await rcon.command("weather.fog 0");
      await rcon.command("weather.rain 0");
      await rcon.command("weather.clouds 0");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/weather/rain", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await rcon.command("weather.rain 1");
      await rcon.command("weather.clouds 0.5");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/weather/fog", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await rcon.command("weather.fog 1");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/weather/storm", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await rcon.command("weather.rain 1");
      await rcon.command("weather.clouds 1");
      await rcon.command("weather.fog 0.5");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .post("/api/plugins/redownload", async ({ headers }) => {
    const blocked = authGuard(headers);
    if (blocked) return blocked;
    try {
      await execInServer(["bash", "/scripts/install-plugins.sh"]);
      await rcon.command("oxide.reload *");
    } catch {}
    return new Response(null, { status: 302, headers: { Location: "/dashboard" } });
  })

  .listen({ port: PORT, serverName: "RustGG" });

console.log(`RustGG admin dashboard running at http://localhost:${PORT}`);
