// ─── Periodic update checker ─────────────────────────────────────────────────
// Opt-in background loop that polls for a newer Rust build (build-id compare via
// docker.checkForUpdate). When one appears, it warns players over RCON on a countdown,
// then restarts the container — the intelligent entrypoint downloads the new build on boot.
//
// Enable with RUST_UPDATE_CHECK_ENABLED=1. Modeled on Didstopia/rust-server's scheduler.

import { checkForUpdate, getServerStatus, restartServer } from "./docker";
import type { RconClient } from "./rcon";
import * as webLog from "./logger";

const ENABLED = process.env.RUST_UPDATE_CHECK_ENABLED === "1";
const INTERVAL_MIN = Math.max(1, parseInt(process.env.RUST_UPDATE_CHECK_INTERVAL || "15") || 15);
const DELAY_MIN = Math.max(0, parseInt(process.env.RUST_UPDATE_RESTART_DELAY || "5") || 5);

// Countdown checkpoints (seconds before restart) at which players are warned.
const WARN_AT_SEC = [300, 180, 60, 30, 10];

interface SchedulerState {
  enabled: boolean;
  intervalMin: number;
  delayMin: number;
  restartScheduled: boolean;
  restartAt: number | null; // epoch ms of the pending restart
  pendingBuild: string | null;
}

const state: SchedulerState = {
  enabled: ENABLED,
  intervalMin: INTERVAL_MIN,
  delayMin: DELAY_MIN,
  restartScheduled: false,
  restartAt: null,
  pendingBuild: null,
};

let timers: ReturnType<typeof setTimeout>[] = [];
let checking = false;
// Build the admin cancelled — don't auto-reschedule it until a newer build appears.
let snoozedBuild: string | null = null;
let rcon: RconClient | null = null;

export function getSchedulerStatus(): SchedulerState {
  return { ...state };
}

export function startUpdateScheduler(rconClient: RconClient): void {
  rcon = rconClient;
  if (!ENABLED) {
    webLog.info("update", "Auto-update checker disabled (set RUST_UPDATE_CHECK_ENABLED=1 to enable)");
    return;
  }
  webLog.info(
    "update",
    `Auto-update checker on: every ${INTERVAL_MIN}m, ${DELAY_MIN}m player warning before restart`
  );
  setTimeout(runCheck, 120_000); // first check ~2 min after boot (let the server settle)
  setInterval(runCheck, INTERVAL_MIN * 60_000);
}

async function runCheck(): Promise<void> {
  if (state.restartScheduled || checking) return;
  checking = true;
  try {
    const status = await getServerStatus();
    if (!status.running) return; // need a running container to exec the check / warn players
    const info = await checkForUpdate();
    if (!info.updateAvailable || !info.latest) return;
    if (info.latest === snoozedBuild) {
      webLog.info("update", `Update ${info.latest} available but snoozed (cancelled by admin); skipping`);
      return;
    }
    webLog.warn("update", `New build available (${info.installed || "none"} -> ${info.latest}); scheduling warned restart`);
    scheduleWarnedRestart(info.latest);
  } catch (e: any) {
    webLog.error("update", `Auto-update check failed: ${e.message}`);
  } finally {
    checking = false;
  }
}

function say(msg: string): Promise<string> {
  if (!rcon) return Promise.resolve("");
  return rcon.command(`say "${msg.replace(/"/g, "'")}"`).catch(() => "");
}

function scheduleWarnedRestart(latest: string): void {
  if (state.restartScheduled) return;
  state.restartScheduled = true;
  state.pendingBuild = latest;
  const delayMs = DELAY_MIN * 60_000;
  state.restartAt = Date.now() + delayMs;

  if (delayMs > 0) {
    say(`A server update (build ${latest}) is available — restarting in ${DELAY_MIN} minute${DELAY_MIN === 1 ? "" : "s"} to apply it.`);
    for (const sec of WARN_AT_SEC) {
      const fireAt = delayMs - sec * 1000;
      if (fireAt <= 0) continue; // skip checkpoints at/after the initial announcement
      timers.push(setTimeout(() => say(`Server restarting for update in ${fmtDuration(sec)}…`), fireAt));
    }
  }
  timers.push(setTimeout(doRestart, delayMs));
}

async function doRestart(): Promise<void> {
  try {
    await say("Server is restarting now to apply the update — back in a few minutes!");
    webLog.warn("update", `Restarting to apply update (build ${state.pendingBuild})`);
    await restartServer();
  } catch (e: any) {
    webLog.error("update", `Update restart failed: ${e.message}`);
  } finally {
    clearSchedule();
  }
}

// Cancel a pending auto-restart. Called by the dashboard's Cancel button and whenever the
// admin takes a manual lifecycle action (so the countdown can't fire on top of them).
export function cancelScheduledRestart(reason: "admin" | "manual-action" = "admin"): boolean {
  if (!state.restartScheduled) return false;
  const build = state.pendingBuild;
  snoozedBuild = build; // don't re-trigger this exact build automatically
  clearSchedule();
  webLog.warn("update", `Scheduled update restart (build ${build}) cancelled (${reason})`);
  if (reason === "admin") say("Scheduled update restart has been cancelled.");
  return true;
}

function clearSchedule(): void {
  for (const t of timers) clearTimeout(t);
  timers = [];
  state.restartScheduled = false;
  state.restartAt = null;
  state.pendingBuild = null;
}

function fmtDuration(sec: number): string {
  if (sec >= 60) {
    const m = Math.round(sec / 60);
    return `${m} minute${m === 1 ? "" : "s"}`;
  }
  return `${sec} second${sec === 1 ? "" : "s"}`;
}
