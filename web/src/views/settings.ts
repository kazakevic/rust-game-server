import { layout } from "./layout";
import { pageHeader, section, input, textarea, select, checkbox, button, alert } from "./components";

export interface ServerSettings {
  serverName: string;
  serverIdentity: string;
  serverDescription: string;
  serverUrl: string;
  serverHeaderImage: string;
  serverLogoImage: string;
  serverTags: string;
  mapSeed: number;
  worldSize: number;
  maxPlayers: number;
  saveInterval: number;
  serverPort: number;
  queryPort: number;
  rconPort: number;
  appPort: number;
  tickrate: number;
  globalChat: boolean;
  stability: boolean;
  radiation: boolean;
  idleKick: number;
  idleKickMode: string;
  updateOnStart: boolean;
  umodEnabled: boolean;
  serverMode: string;
}

export function settingsPage(data: { settings: ServerSettings | null; error?: string; success?: string }) {
  const s = data.settings;

  const content = `
    ${pageHeader("Server Settings", { description: "Configure server startup parameters and game mode." })}

    ${alert("These settings are applied when the server starts. After saving, restart the server for changes to take effect.", "warning")}

    ${data.error ? `<div class="mt-4">${alert(data.error, "error")}</div>` : ""}
    ${data.success ? `<div class="mt-4">${alert('Settings saved successfully. <form method="POST" action="/api/server/restart" style="display:inline"><button type="submit" class="underline font-medium hover:opacity-80">Restart server now</button></form> to apply changes.', "success")}</div>` : ""}

    <form method="POST" action="/api/server/settings/save" class="mt-6 space-y-6">

      ${section("Server Identity", `
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          ${input({ name: "serverName", label: "Server Name", value: s?.serverName ?? "", placeholder: "My Rust Server" })}
          ${input({ name: "serverIdentity", label: "Server Identity", value: s?.serverIdentity ?? "", hint: "Changing this creates a new world save folder." })}
        </div>
      `)}

      ${section("Server Branding", `
        <div class="space-y-4">
          ${textarea({ name: "serverDescription", label: "Server Description", value: s?.serverDescription ?? "", placeholder: "Welcome to our server!\\nWipe: Weekly on Thursdays\\nRules: No cheating", rows: 4, hint: "Shown on the server info page. Use \\\\n for line breaks." })}
          ${input({ name: "serverUrl", label: "Website URL", value: s?.serverUrl ?? "", placeholder: "https://yoursite.com", hint: "Displayed as the \"Webpage\" link on the server info page." })}
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            ${input({ name: "serverHeaderImage", label: "Header Image URL", value: s?.serverHeaderImage ?? "", placeholder: "https://i.imgur.com/example.png", hint: "Banner image: 1024\u00d7512 px recommended. Direct .jpg/.png URL." })}
            ${input({ name: "serverLogoImage", label: "Logo Image URL", value: s?.serverLogoImage ?? "", placeholder: "https://i.imgur.com/example.png", hint: "Server icon: 256\u00d7256 px. Displayed as circle in browser & Rust+." })}
          </div>
          ${input({ name: "serverTags", label: "Server Tags", value: s?.serverTags ?? "", placeholder: "weekly,vanilla", hint: "Comma-separated. Wipe: monthly/biweekly/weekly. Type: vanilla/pve/roleplay/creative/minigame/battlefield. Mod: modded/oxide/carbon." })}
        </div>
      `, { description: "Customize how your server appears in the server browser." })}

      ${section("World", `
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label class="block text-sm font-medium text-zinc-700 mb-1.5">Map Seed</label>
            <div class="flex gap-2">
              <input type="number" name="mapSeed" value="${s?.mapSeed ?? 12345}"
                     class="flex h-9 w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm transition-colors placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent" />
              ${button("Random", { variant: "outline", size: "sm", attrs: 'type="button" id="random-seed"' })}
            </div>
            <p class="mt-1 text-xs text-zinc-400">0 \u2013 2147483647</p>
          </div>
          ${input({ name: "worldSize", label: "World Size", type: "number", value: String(s?.worldSize ?? 3500), hint: "1000 \u2013 6000 (default 3500)" })}
          ${input({ name: "maxPlayers", label: "Max Players", type: "number", value: String(s?.maxPlayers ?? 100) })}
        </div>
      `)}

      ${section("Server Mode", `
        <div class="max-w-sm">
          <label class="block text-sm font-medium text-zinc-700 mb-1.5">Game Mode</label>
          ${select({
            name: "serverMode",
            items: [
              { value: "vanilla", label: "Vanilla", selected: s?.serverMode === "vanilla" || !s?.serverMode },
              { value: "pve", label: "PvE", selected: s?.serverMode === "pve" },
              { value: "softcore", label: "Softcore", selected: s?.serverMode === "softcore" },
              { value: "creative", label: "Creative", selected: s?.serverMode === "creative" },
              { value: "gungame", label: "GunGame", selected: s?.serverMode === "gungame" },
            ],
            class: "w-full",
          })}
          <div class="mt-3 text-xs text-zinc-500 space-y-1">
            <p><span class="font-medium text-zinc-600">Vanilla</span> \u2014 Default Rust experience</p>
            <p><span class="font-medium text-zinc-600">PvE</span> \u2014 No player damage (server.pve true)</p>
            <p><span class="font-medium text-zinc-600">Softcore</span> \u2014 Built-in Rust softcore mode</p>
            <p><span class="font-medium text-zinc-600">Creative</span> \u2014 PvE + instant craft + no decay</p>
            <p><span class="font-medium text-zinc-600">GunGame</span> \u2014 Uses GunGame plugin for progression</p>
          </div>
        </div>
      `)}

      ${section("Gameplay", `
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          ${input({ name: "saveInterval", label: "Auto-Save Interval", type: "number", value: String(s?.saveInterval ?? 600), hint: "Seconds between saves (default 600)" })}
          ${input({ name: "tickrate", label: "Server Tickrate", type: "number", value: String(s?.tickrate ?? 30), hint: "Updates per second (max 30)" })}
          ${input({ name: "idleKick", label: "Idle Kick (minutes)", type: "number", value: String(s?.idleKick ?? 30), hint: "0 = disabled" })}
        </div>
        <div class="mt-4 max-w-sm">
          <label class="block text-sm font-medium text-zinc-700 mb-1.5">Idle Kick Mode</label>
          ${select({
            name: "idleKickMode",
            items: [
              { value: "0", label: "Disabled", selected: String(s?.idleKickMode) === "0" },
              { value: "1", label: "When server is full", selected: String(s?.idleKickMode ?? "1") === "1" },
              { value: "2", label: "Always", selected: String(s?.idleKickMode) === "2" },
            ],
            class: "w-full",
          })}
        </div>
        <div class="mt-4 space-y-3">
          ${checkbox({ name: "globalChat", label: "Global chat (all players see messages)", checked: s?.globalChat ?? true })}
          ${checkbox({ name: "stability", label: "Building stability system", checked: s?.stability ?? true })}
          ${checkbox({ name: "radiation", label: "Radiation zones enabled", checked: s?.radiation ?? true })}
        </div>
      `, { description: "In-game behavior and performance settings." })}

      ${section("Network", `
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          ${input({ name: "serverPort", label: "Server Port", type: "number", value: String(s?.serverPort ?? 28015) })}
          ${input({ name: "queryPort", label: "Query Port", type: "number", value: String(s?.queryPort ?? 28017) })}
          ${input({ name: "rconPort", label: "RCON Port", type: "number", value: String(s?.rconPort ?? 28016) })}
          ${input({ name: "appPort", label: "App Port", type: "number", value: String(s?.appPort ?? 28082) })}
        </div>
        <p class="mt-3 text-xs text-zinc-400">Changing ports also requires updating compose.yaml port mappings and restarting the stack.</p>
      `, { description: "Server network configuration" })}

      ${section("Startup Options", `
        <div class="space-y-3">
          ${checkbox({ name: "updateOnStart", label: "Update server via SteamCMD on start", checked: s?.updateOnStart ?? true })}
          ${checkbox({ name: "umodEnabled", label: "Enable uMod (Oxide) plugin framework", checked: s?.umodEnabled ?? true })}
        </div>
      `)}

      <div class="flex justify-end">
        ${button("Save Settings", { type: "submit", variant: "primary", size: "lg" })}
      </div>
    </form>

    <script>
      document.getElementById('random-seed').addEventListener('click', function() {
        document.querySelector('input[name="mapSeed"]').value = Math.floor(Math.random() * 2147483647);
      });
    </script>
  `;

  return layout("Server Settings", content, { activePage: "settings" });
}
