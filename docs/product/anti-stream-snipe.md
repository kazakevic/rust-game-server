# The Stream-Sniping Problem & Anti-Snipe Spec

The core problem rust-gg exists to solve, why every existing defense fails, and the layered
server-side solution we will build. This is the heart of the product's
[differentiation](vision.md#why-we-win-differentiation).

> **Status:** this document **specifies** the approach. None of it is built yet — this round
> is documentation only. Implementation is planned in a later round; the spec maps onto the
> existing Docker/entrypoint + `umod-plugins.txt` + Bun/Elysia web-admin stack (see
> [architecture.md](../architecture.md)).

## What stream sniping is, and why Rust is uniquely exposed

**Stream sniping** is watching a player's live broadcast to gain information their opponents
shouldn't have — server name, player count, names, branding, and any on-screen environmental
detail — then exploiting it for combat advantage, raid timing, harassment, or even
[DDoS attacks](https://www.corrosionhour.com/rust-streamer-mode-command/).

Rust is **especially vulnerable** because it is a persistent survival game: a single wipe
represents tens-to-hundreds of hours of progress, so knowing a creator's exact base
location, grid, loadout, online/offline timing, and even facing direction is decisive
([ACHIVX](https://achivx.com/can-you-get-banned-for-stream-sniping-in-rust/)). The in-game
**map grid is 150m × 150m**
([CorrosionHour](https://www.corrosionhour.com/rust-map-guide/)), so snipers triangulate
exact positions from the map + landmarks shown on stream. It takes several forms: location
sniping, server identification → join-to-hunt, coordinated clan call-outs, kill-on-sight
harassment, and **raid sniping** while the creator is offline.

## Why native defenses fail

- **Streamer Mode is client-side and reversible.** Garry Newman added native Streamer Mode
  in [2015](https://kotaku.com/cheaters-convinced-rust-creator-to-add-anti-stream-snip-1732164331)
  (Rust was the first game to build anti-sniping in natively) and
  [expanded it on Jan 30 2021](https://www.pcgamesn.com/rust/rust-streamers-can-now-conceal-their-location-from-griefers)
  to hide server names/descriptions and randomize player names. But the aliases are
  **deterministically derived from SteamID**, so public tools reproduce the function and
  unmask anyone — [rust-de-stream-mode](https://github.com/realstrings/rust-de-stream-mode),
  [rfelf/rust-streamer-names](https://github.com/rfelf/rust-streamer-names),
  [rustdecoder.com](https://www.rustdecoder.com/). It is also **toggled client-side only**
  (cannot be set via RCON) and does **not** hide: the in-game map, your real Steam name when
  someone loots your corpse, modded-server killfeeds/chat/bots, or your Steam name on the
  server-select screen ([CorrosionHour](https://www.corrosionhour.com/rust-streamer-mode-command/),
  [Glimpse](https://glimpse.me/blog/rust-streamer-mode/)).
- **Third-party trackers defeat it entirely.** See
  [Market & Competition → tracking vector](market-and-competition.md#the-third-party-tracking-vector).
- **Stream delay defeats real-time sniping but kills engagement.** A 20–30s+ delay makes
  screen-sniping near-impossible but breaks chat interaction, polls, donations, and sub-goals
  ([Hexeum](https://hexeum.net/guides/stream-delay-on-twitch/),
  [Streamlabs](https://streamlabs.com/content-hub/post/how-to-reduce-stream-delay)) — which
  is why it's reserved for tournaments.
- **Enforcement is near-impossible.** Proof generally requires the streamer to bait a trap
  (the [Mendo case](https://www.dexerto.com/rust/mendo-gets-rust-stream-sniper-banned-after-setting-up-the-perfect-trap-1610427/),
  where calling out "roof campers" aloud without using in-game chat exposed the sniper) plus
  a lucky admin. Many large communities won't even attempt it.

## Documented harm

This is not theoretical. The [OfflineTV server drama](https://gamerant.com/rust-offlinetv-server-twitch-streamers-controversy-explained/)
(Jan 2021) produced public sniping accusations, **death threats** (ash_on_lol), and Pokimane
leaving; sniping persisted even into the officially-organized
[Twitch Rivals event in May 2023](https://www.essentiallysports.com/esports-news-they-are-absolute-garbage-at-their-job-xqc-bashes-streamers-who-allegedly-stream-sniped-him-in-rust-at-twitch-rivals/)
(xQc); and it routinely escalates to coordinated clan harassment and DDoS — a
mental-health and safety issue, not just a competitive one.

## Our approach: layered, server-side anti-snipe

Vanilla Rust has **no native password** — privacy requires an Oxide whitelist (e.g.
[Wulf's Whitelist](https://umod.org/plugins/whitelist)) and/or a hidden server. Facepunch
itself documents the
[hidden + whitelisted setup](https://wiki.facepunch.com/rust/Creating_a_hidden_whitelisted_server).
We combine these and add the server-side pieces nobody packages today.

| Priority | Capability | Why it matters / how |
|---|---|---|
| **P0** | **Hidden + whitelisted foundation** | Block the Steam queryport, use non-standard server/rcon ports, Oxide whitelist → absent from BattleMetrics/RUSTalyzer, joinable only by verified creators. Facepunch documents this exact setup. |
| **P0** | **Server-side per-session name salting** | Non-deterministic display names so SteamID→alias reversers fail. **No plugin does this today** → a custom Oxide plugin (modular-plugin pattern). The headline differentiator, impossible client-side. |
| **P0** | **`connecthidden` onboarding + reserved queue slot** | Join off-stream without exposing the IP (`client.connecthidden`); guaranteed entry via native `global.skipqueueid` / [Priority Queue](https://codefling.com/plugins/priority-queue) / [Ultimate Queue](https://umod.org/plugins/ultimate-queue). |

| **P1** | **Server-side information obfuscation** | Delayed/anonymized killfeeds, hidden/delayed heli/cargo/raid alerts, suppressed join/leave, hidden live player count — the stream-delay benefit without the engagement cost. |
| **P1** | **Moderation dashboard + community ban DB** | Cross-server ban database (Server Armour-style) + account-age gating on top of EAC + incident log + spectate hooks, in the web admin. Fast/fair moderation is the top trust signal. |
| **P1** | **Auto IP-rotation / leak-response runbook** | Facepunch concedes IP leakage is "extremely likely"; detect a leak and switch IPs fast (keep spare IPs). Feasible on the Docker/Dokploy + entrypoint stack. |
| **P1** | **Anti-DDoS hosting + performance discipline** | DDoS is a near-daily streamer problem; lag ruins PvP clips. Lean plugin budget, high-clock single-thread-aware CPU host. |
| **P2** | **Creator wipe events + Drops + tuned ruleset** | Synchronized events timed to Twitch/Kick Drops; group-size caps / wipe cadence / anti-zerg guardrails to maximize watchable content and retention. |

Sequencing and the (deferred) monetization layer are in [roadmap.md](roadmap.md).
