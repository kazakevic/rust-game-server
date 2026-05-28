# Market & Competition

The size of the Rust creator opportunity and a teardown of the servers creators play on
today. Strategy built on this data lives in [vision.md](vision.md).

## Market opportunity

Rust is a large, durable, creator-driven ecosystem in a **growth/peak phase, not decline**:

- **20M+ copies sold** (July 2025), **$2B+ lifetime gross revenue**.
- **All-time peak of 262,284 concurrent Steam players on Jan 2 2025** — more than double the
  2021 record — with ~95K–124K average concurrents through 2024–25.
- On Twitch, Rust is a consistent **top-15 game** (~13K–18K average concurrent viewers,
  tens of millions of hours watched), with event-driven spikes: Twitch Rivals Team Battle V
  peaked **186,957**, and the 2021 OfflineTV server peaked **~1.2–1.3M**.

This makes Rust streaming **mature/stable-with-spikes** — steady, monetizable creator
demand rather than a passing fad.

### Where the addressable market is

The realistic addressable market is the **low thousands of monetizable creators** (out of
~8,400 Rust Twitch streamers plus thousands of YouTubers). Only ~10–20 channels exceed 500K
followers — but **hundreds-to-low-thousands sit in the monetizable 1K–50K /
100–1,500-avg-viewer band**, and that mid-tier is the underserved beachhead (see
[vision.md → Who it's for](vision.md#who-its-for)).

Demand is **hard-validated**, not hypothetical: top creators repeatedly hand-build temporary
whitelisted "play without getting sniped" servers
([hJune's 300+ creator server](https://x.com/h7une/status/1305834034719719425)), and there
is a standing community request to
[exclude streamer servers from BattleMetrics tracking](https://ideas.battlemetrics.com/263).

As a solo indie product, capturing even a few hundred mid-tier creators/squads is a viable,
bootstrappable business; a single mega-creator endorsement or hosted event could vault it to
OTV-scale visibility.

## Competitive landscape

The "Rust streamer server" market splits into three models — **none of which owns
anti-snipe as a core value proposition.**

| Model | Examples | Positioning | Anti-snipe? |
|---|---|---|---|
| **Public vanilla networks** | [Rustafied](https://www.rustafied.com/server) (30+ servers, segmented by population & wipe cadence, 3pm-local wipes), Rustoria (vanilla, weekly Thursday wipe), Rustopia (Oxide-modded "vanilla-feel", monthly) | Compete on **population, region coverage, and vanilla purity** | **No** — publicly listed and trackable; Rustafied [explicitly won't ban for stream sniping](https://forum.rustafied.com/faq/moderation/banning/why-don%E2%80%99t-you-ban-for-stream-sniping-r54/) |
| **One-off whitelisted creator events** | [OfflineTV/OTV](https://streamscharts.com/news/rust-revival-twitch) (Dec 2020–Jan 2021; made Rust #1 on Twitch, ~1.3M peak, then imploded over sniping + PvP/RP tension into "Badlands" & "Divide"), EGOLAND (Spain), hJune's 300+ server, Twitch Rivals | Invite-only, "play without getting sniped" | **Partial** — whitelisted, but **temporary, manually curated, and still leak** via in-game mechanics and third-party trackers |
| **Creator-branded modded servers** | Single-personality servers tied to one creator's audience | Audience-driven, often modded (kits/shop) | No |

## The third-party tracking vector

Even a whitelisted server leaks through trackers that operate **outside** the game client —
**[BattleMetrics](https://learn.battlemetrics.com/article/44-what-can-i-do-to-hide-my-player-profile)**,
RUSTalyzer, Just-Wiped, tsarvar — which publicly expose server IP, current player list,
population graphs, session logs (join/leave timing), and wipe schedules. This is the #1
sniping vector that client-side Streamer Mode cannot touch.

> **Nuance:** BattleMetrics added an opt-in, **admin-side** `clientperf` integration that
> hides a player's *live* status on participating servers — but it does not hide session
> *history*, and the vast majority of servers don't enable it. The robust fix is to be
> **unlisted by design** (hidden + whitelisted), where trackers have nothing to index.

## The gap

**No major commercial server productizes a persistent, properly hidden/whitelisted,
server-side anti-snipe environment as its core value proposition.** Public networks compete
on population; creator events are temporary and manual. That whitespace — a *permanent,
untracked, snipe-proof creator home* — is the opportunity rust-gg can own. The technical
shape of that solution is specified in [anti-stream-snipe.md](anti-stream-snipe.md).
