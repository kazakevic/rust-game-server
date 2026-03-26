import { layout } from "./layout";

interface ConfigData {
  config: Record<string, any> | null;
  error?: string;
  success?: string;
}

export function configPage(data: ConfigData) {
  const { config, error, success } = data;

  const banner = error
    ? `<div class="bg-red-900/50 border border-red-700 text-red-300 px-4 py-3 rounded mb-6">${error}</div>`
    : success
    ? `<div class="bg-green-900/50 border border-green-700 text-green-300 px-4 py-3 rounded mb-6">${success}</div>`
    : "";

  if (!config) {
    return layout("Config", `
      <h2 class="text-2xl font-bold mb-6">GunGame Config</h2>
      ${banner || '<div class="bg-yellow-900/50 border border-yellow-700 text-yellow-300 px-4 py-3 rounded">Could not load config. Is the server running?</div>'}
    `);
  }

  return layout("Config", `
    <h2 class="text-2xl font-bold mb-6">GunGame Config</h2>
    ${banner}
    <form method="POST" action="/api/config/save" class="space-y-6">

      ${section("XP Settings", [
        field("XPPerKill", config.XPPerKill, "number", "Base XP per player kill"),
        field("HeadshotBonusXP", config.HeadshotBonusXP, "number", "Bonus XP for headshot"),
        field("DistanceBonusXPPer50m", config.DistanceBonusXPPer50m, "number", "Bonus XP per 50m distance"),
        field("XPPerAnimalKill", config.XPPerAnimalKill, "number", "XP per animal kill"),
        field("XPPerNPCKill", config.XPPerNPCKill, "number", "XP per NPC kill"),
      ])}

      ${section("Kill Reward", [
        field("KillRewardItemShortname", config.KillRewardItemShortname, "text", "Item shortname (empty to disable)"),
        field("KillRewardMinAmount", config.KillRewardMinAmount, "number", "Minimum amount"),
        field("KillRewardMaxAmount", config.KillRewardMaxAmount, "number", "Maximum amount"),
      ])}

      ${section("Progression", [
        field("MaxLevel", config.MaxLevel, "number", "Maximum level"),
        field("DifficultyMultiplier (scales XP thresholds: 0.5=easy, 1.0=normal, 2.0=hard)", config["DifficultyMultiplier (scales XP thresholds: 0.5=easy, 1.0=normal, 2.0=hard)"], "number", "XP threshold multiplier (0.5=easy, 2.0=hard)", "0.1"),
        field("KitPrefix", config.KitPrefix, "text", "Kit name prefix"),
      ])}

      ${section("General", [
        field("ChatPrefix", config.ChatPrefix, "text", "Chat message prefix"),
        field("TopListSize", config.TopListSize, "number", "Leaderboard entries"),
        fieldCheckbox("WipeOnNewSave", config.WipeOnNewSave, "Wipe data on new map save"),
      ])}

      ${section("Level XP Thresholds", [
        thresholdsFields(config.LevelXPThresholds || {}),
      ])}

      <div class="flex gap-3">
        <button type="submit" class="bg-green-600 hover:bg-green-700 text-white px-6 py-2 rounded font-medium">
          Save & Reload Plugin
        </button>
        <a href="/config" class="bg-gray-700 hover:bg-gray-600 text-white px-6 py-2 rounded font-medium">Reset Form</a>
      </div>
    </form>
  `);
}

function section(title: string, fields: string[]) {
  return `
    <div class="bg-gray-900 border border-gray-800 rounded-lg p-6">
      <h3 class="text-lg font-semibold mb-4">${title}</h3>
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        ${fields.join("")}
      </div>
    </div>`;
}

function field(name: string, value: any, type: string, label: string, step?: string) {
  const stepAttr = step ? ` step="${step}"` : type === "number" ? ' step="1"' : "";
  return `
    <div>
      <label class="block text-sm text-gray-400 mb-1">${label}</label>
      <input type="${type}" name="${name}" value="${escapeHtml(String(value ?? ""))}"
             class="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-gray-200 focus:outline-none focus:border-rust-400"
             ${stepAttr}>
    </div>`;
}

function fieldCheckbox(name: string, checked: boolean, label: string) {
  return `
    <div class="flex items-center gap-2 col-span-full">
      <input type="hidden" name="${name}" value="false">
      <input type="checkbox" name="${name}" value="true" ${checked ? "checked" : ""}
             class="w-4 h-4 rounded bg-gray-800 border-gray-700">
      <label class="text-sm text-gray-400">${label}</label>
    </div>`;
}

function thresholdsFields(thresholds: Record<string, number>) {
  const sorted = Object.entries(thresholds).sort(([a], [b]) => parseInt(a) - parseInt(b));
  const fields = sorted.map(([level, xp]) => `
    <div class="flex items-center gap-2">
      <span class="text-sm text-gray-500 w-20">Level ${level}</span>
      <input type="number" name="threshold_${level}" value="${xp}" step="100"
             class="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-gray-200 focus:outline-none focus:border-rust-400">
    </div>
  `).join("");
  return `<div class="col-span-full grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">${fields}</div>`;
}

function escapeHtml(s: string) {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
