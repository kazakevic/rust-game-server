import { layout } from "./layout";
import { pageHeader, alert, section, input, checkbox, button, escapeHtml } from "./components";

interface ConfigData {
  config: Record<string, any> | null;
  error?: string;
  success?: string;
}

export function configPage(data: ConfigData) {
  const { config, error, success } = data;

  const banner = error
    ? alert(error, "error")
    : success
    ? alert(success, "success")
    : "";

  if (!config) {
    return layout("Config", `
      ${pageHeader("GunGame Config", { description: "Configure the GunGame plugin settings" })}
      ${banner || alert("Could not load config. Is the server running?", "warning")}
    `, { activePage: "config" });
  }

  return layout("Config", `
    ${pageHeader("GunGame Config", { description: "Configure the GunGame plugin settings" })}
    ${banner ? `<div class="mb-6">${banner}</div>` : ""}

    <form method="POST" action="/api/config/gungame/save" class="space-y-6">

      ${section("XP Settings", `
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          ${input({ name: "XPPerKill", label: "Base XP per player kill", type: "number", value: String(config.XPPerKill ?? "") })}
          ${input({ name: "HeadshotBonusXP", label: "Bonus XP for headshot", type: "number", value: String(config.HeadshotBonusXP ?? "") })}
          ${input({ name: "DistanceBonusXPPer50m", label: "Bonus XP per 50m distance", type: "number", value: String(config.DistanceBonusXPPer50m ?? "") })}
          ${input({ name: "XPPerAnimalKill", label: "XP per animal kill", type: "number", value: String(config.XPPerAnimalKill ?? "") })}
          ${input({ name: "XPPerNPCKill", label: "XP per NPC kill", type: "number", value: String(config.XPPerNPCKill ?? ""), class: "md:col-span-1" })}
        </div>
      `, { description: "Points earned for different kill types" })}

      ${section("Kill Reward", `
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          ${input({ name: "KillRewardItemShortname", label: "Item shortname", type: "text", value: String(config.KillRewardItemShortname ?? ""), hint: "Leave empty to disable" })}
          ${input({ name: "KillRewardMinAmount", label: "Minimum amount", type: "number", value: String(config.KillRewardMinAmount ?? "") })}
          ${input({ name: "KillRewardMaxAmount", label: "Maximum amount", type: "number", value: String(config.KillRewardMaxAmount ?? "") })}
        </div>
      `, { description: "Item reward given on player kill" })}

      ${section("Progression", `
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          ${input({ name: "MaxLevel", label: "Maximum level", type: "number", value: String(config.MaxLevel ?? "") })}
          ${input({ name: "DifficultyMultiplier (scales XP earned: 0.5=hard, 1.0=normal, 2.0=easy)", label: "Difficulty multiplier", type: "number", value: String(config["DifficultyMultiplier (scales XP earned: 0.5=hard, 1.0=normal, 2.0=easy)"] ?? ""), step: "0.1", hint: "0.5 = hard, 1.0 = normal, 2.0 = easy" })}
          ${input({ name: "KitPrefix", label: "Kit name prefix", type: "text", value: String(config.KitPrefix ?? "") })}
        </div>
      `, { description: "Level progression and difficulty settings" })}

      ${section("General", `
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          ${input({ name: "ChatPrefix", label: "Chat message prefix", type: "text", value: String(config.ChatPrefix ?? "") })}
          ${input({ name: "TopListSize", label: "Leaderboard entries", type: "number", value: String(config.TopListSize ?? "") })}
        </div>
        <div class="mt-4">
          ${checkbox({ name: "WipeOnNewSave", label: "Wipe player data on new map save", checked: !!config.WipeOnNewSave })}
        </div>
      `, { description: "General plugin configuration" })}

      ${section("Level XP Thresholds", `
        <div id="thresholds-container">
          ${thresholdsGrid(config.LevelXPThresholds || {}, config.MaxLevel ?? 5)}
        </div>
        <script>
          (function() {
            const maxInput = document.querySelector('input[name="MaxLevel"]');
            const container = document.getElementById('thresholds-container');
            if (!maxInput || !container) return;

            function getCurrentValues() {
              const vals = {};
              container.querySelectorAll('input[type="number"]').forEach(inp => {
                const m = inp.name.match(/^threshold_(\\d+)$/);
                if (m) vals[m[1]] = inp.value;
              });
              return vals;
            }

            function rebuild() {
              const max = parseInt(maxInput.value) || 2;
              const vals = getCurrentValues();
              const gridClass = "grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3";
              const inputClass = "flex h-9 w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent";
              let html = '<div class="' + gridClass + '">';
              for (let lvl = 2; lvl <= max; lvl++) {
                const val = vals[lvl] || "";
                html += '<div><label class="block text-xs font-medium text-zinc-500 mb-1">Level ' + lvl + '</label>';
                html += '<input type="number" name="threshold_' + lvl + '" value="' + val + '" step="100" class="' + inputClass + '" /></div>';
              }
              html += '</div>';
              container.innerHTML = html;
            }

            maxInput.addEventListener('input', rebuild);
          })();
        </script>
      `, { description: "XP required to reach each level — adapts to Maximum Level" })}

      <div class="flex items-center gap-3">
        ${button("Save & Reload Plugin", { variant: "primary", size: "lg", type: "submit" })}
        <a href="/config/gungame" class="inline-flex items-center justify-center rounded-lg h-10 px-6 text-sm font-medium border border-zinc-200 bg-white text-zinc-700 hover:bg-zinc-50 transition-colors">Reset Form</a>
      </div>
    </form>
  `, { activePage: "config" });
}

function thresholdsGrid(thresholds: Record<string, number>, maxLevel: number): string {
  const fields: string[] = [];
  for (let level = 2; level <= maxLevel; level++) {
    const xp = thresholds[String(level)] ?? "";
    fields.push(`
      <div>
        <label class="block text-xs font-medium text-zinc-500 mb-1">Level ${level}</label>
        <input type="number" name="threshold_${level}" value="${xp}" step="100"
               class="flex h-9 w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent" />
      </div>
    `);
  }
  return `<div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3">${fields.join("")}</div>`;
}
