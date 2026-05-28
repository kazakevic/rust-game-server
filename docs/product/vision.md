# Vision & Strategy

What rust-gg is for, who it serves, and why it can win. For the market data behind these
claims see [Market & Competition](market-and-competition.md); for the core problem and the
technical approach see [The Stream-Sniping Problem & Anti-Snipe Spec](anti-stream-snipe.md);
for the execution plan see [Product Roadmap](roadmap.md).

## Vision

Become the default **snipe-proof creator home** for Rust: a permanently-on, whitelisted,
untracked, **server-side-protected** server where any creator can play, raid, and feud on
camera without leaking their server, grid location, online/offline status, or identity to
viewers.

We make stream sniping **structurally impossible**, not just cosmetically harder — and we
turn the ad-hoc, hand-rolled "whitelisted creator server" that streamers keep rebuilding by
hand (e.g. hJune's
[300+ creator server](https://x.com/h7une/status/1305834034719719425)) into a turnkey,
always-on product with one-click creator onboarding.

> **Guiding principle:** we sell **safety and convenience, never power.** The server stays
> vanilla-credible; protection and quality-of-life are the value, not pay-to-win advantage.

## The problem in one line

Stream sniping is the single most damaging unsolved problem for Rust creators, and Rust's
native [Streamer Mode](https://www.corrosionhour.com/rust-streamer-mode-command/) is purely
client-side and fundamentally reversible. Full deep-dive: [anti-stream-snipe.md](anti-stream-snipe.md).

## Who it's for

| Tier | Role | Notes |
|---|---|---|
| **Mid-tier / up-and-coming creators** (~1K–50K followers, ~100–1,500 avg viewers) | **Primary beachhead** | Large in number, feel sniping most acutely, and — unlike mega-streamers — lack the clout and technical skill to self-host a whitelisted server. The most underserved, easiest-to-delight segment. |
| Small friend-squads (duos / trios / quads) | Primary | Want a fair, populated, snipe-safe home to make content together across wipes. |
| Spanish-language / LatAm creators | **Secondary beachhead** | Dominate Rust's top avg-viewer charts; under-targeted; worth a dedicated region once the English scene is proven. |
| Twitch Rivals-style event operators | B2B (later) | Recurring $50K–$100K events prove demand; could license/rent the anti-snipe infrastructure. |
| Mega-creators (Welyn, hJune, Spoonkid, Blooprint…) | **Amplifier, not target** | Already self-host — hard to win cold. Won later via word-of-mouth from delighted mid-tier creators and one-off hosted events; a single endorsement is a massive growth catalyst. |

**Explicitly out of scope:** console. PC↔console crossplay is impossible and the native
PS5/Xbox scene (June 2025) is a separate ecosystem — **PC first**.

## Why we win (differentiation)

1. **Server-side per-session salted names** — non-deterministic display names so the
   SteamID→alias reversers that defeat native Streamer Mode
   ([rust-de-stream-mode](https://github.com/realstrings/rust-de-stream-mode) and clones)
   cannot unmask creators. The single most impactful uniquely-offerable feature — and
   **impossible to replicate client-side**.
2. **Untracked by design** — a hidden + whitelisted server (blocked Steam queryport,
   non-standard ports) that deliberately does not appear on BattleMetrics / RUSTalyzer,
   closing the #1 sniping vector (real-time server identification + offline raid-timing).
3. **Server-side information obfuscation** — suppressed/delayed killfeeds, hidden/delayed
   event announcements (heli/cargo/raid), suppressed join/leave broadcasts, hidden live
   player count. Delivers the benefit of stream delay **without** its
   [engagement cost](https://hexeum.net/guides/stream-delay-on-twitch/).
4. **Turnkey creator onboarding** — self-serve verification → auto-whitelist →
   `connecthidden` join → reserved queue slot. The DIY burden made one-click.
5. **Fast, transparent, fair moderation as a trust product** — a cross-server community ban
   database + account-age gating + published impartial admin rules. Fair moderation is a
   stronger trust signal than any single plugin, and admin abuse is reputation-fatal.
6. **Ethical, vanilla-credible stance** — cosmetic/convenience only, never pay-to-win,
   never unowned DLC — preserving the competitive credibility that drives creator clout.

## Success metrics

**North-star:** number of **verified active creators playing per wipe** (target a few dozen
within 6 months, low hundreds within 12–18 months).

Supporting KPIs:

- **Cross-wipe creator retention > 50%** — the real measure of product-market fit.
- **Zero confirmed server-side-leak snipes per wipe** — the core promise; root-cause every
  reported incident.
- **Sustained average concurrents through a wipe cycle** — not just a wipe-day spike that
  dies within 48h.
- **Report time-to-resolution** for cheat/harassment (minutes–hours, human response).
- **Server FPS / tick health** (no rubberbanding events — lag ruins clips).
- **Uptime / DDoS-mitigation success** (no stream-ending outages).
- **Creator referral rate** and any mega-creator endorsement.
- **Hours-watched / clips generated** on the server (free marketing + content-value proxy).

## Top risks

Cold-start chicken-and-egg (no founding-creator anchor), IP leakage on a hidden server
(["extremely likely" per Facepunch](https://wiki.facepunch.com/rust/Creating_a_hidden_whitelisted_server)),
and solo-dev bandwidth. Full risk register with mitigations: [roadmap.md](roadmap.md#risks--mitigations).
