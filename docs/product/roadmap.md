# Product Roadmap

Phased execution from proof-of-concept to category leader, with per-phase KPIs, the risk
register, and the deferred monetization model. Strategy context: [vision.md](vision.md);
the capabilities referenced below are specified in [anti-stream-snipe.md](anti-stream-snipe.md).

## Phases

### Phase 0 — Foundation & proof-of-concept (Weeks 0–6)
- Stand up the **hidden + whitelisted server** on the existing Docker/Dokploy stack (block
  queryport, non-standard ports, Oxide whitelist); confirm it stays off
  BattleMetrics/RUSTalyzer while remaining joinable via `connecthidden`.
- Ship the **P0 per-session name-salting** Oxide plugin; validate it defeats SteamID→alias
  reversers.
- Wire **reserved queue slot + `connecthidden` onboarding**; add basic whitelist management
  to the Bun/Elysia web admin.
- Run a **private pilot wipe** with one design-partner squad / mid-tier creator; instrument
  zero-leak tracking and gather testimonials + clips.

**Exit KPI:** a clean pilot wipe with zero server-side-leak snipes and a usable testimonial.

### Phase 1 — Productize the creator experience (Months 2–4)
- Build **self-serve creator verification → auto-whitelist** (link Twitch/YouTube) with a
  sensible verification threshold.
- Ship **P1 server-side info obfuscation** (delayed killfeeds, hidden/delayed events,
  suppressed join/leave, hidden player count).
- Stand up the **moderation dashboard + cross-server ban DB + account-age gating** and the
  **leak-response/IP-rotation runbook**; move to DDoS-protected hosting; lock in performance
  discipline.
- Onboard the **first cohort of mid-tier creators by referral**; publish transparent rules.

**Exit KPI:** first referral cohort onboarded; moderation TTR in minutes–hours.

### Phase 2 — Grow population (Months 4–8)
- Tune the **creator ruleset** (group caps, wipe cadence, anti-zerg, progression guardrails)
  on retention data.
- Run the **first synchronized creator wipe event** timed to Twitch/Kick Drops; pursue
  Support-a-Streamer alignment; convert event population into recurring verified creators.

**Exit KPI:** a few dozen verified active creators per wipe, **>50% cross-wipe retention**,
zero server-side-leak snipes. **The server remains free throughout this phase.**

### Phase 3 — Own the category & scale (Months 8–18)
- Scale to **low-hundreds of verified creators**; add the **LatAm beachhead** (region/server)
  and evaluate a second English region.
- Win a **mega-creator endorsement** and/or host a marquee event for an OTV-scale visibility
  moment.
- Offer **B2B anti-snipe infrastructure licensing/hosting** to event operators (Twitch
  Rivals-style).
- Establish rust-gg as the recognized **"snipe-proof creator server"** brand with a
  published incident track record and a repeatable onboarding + wipe-event playbook.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| **Cold-start chicken-and-egg** — creators want population, population follows creators, and a non-celebrity solo operator has no founding anchor | Start narrow: one design-partner squad/creator, a free invite-only pilot wipe, capture clips/testimonials, expand by referral; time launches to Twitch/Kick Drops |
| **IP leakage** is ["extremely likely" per Facepunch](https://wiki.facepunch.com/rust/Creating_a_hidden_whitelisted_server) — one leak breaks the promise mid-stream | P1 auto IP-rotation/leak-response runbook, spare IPs/ports ready, queryport blocked always, DDoS-protected hosting; set honest defense-in-depth expectations; treat any leak as a P0 incident |
| **Anti-cheat ceiling** — EAC misses external/screen-reading cheats and ban-waves monthly; one hacker is reputation-fatal | Whitelisting filters most bad actors; layer a community ban DB + account-age gating + fast human moderation + spectate tools; set "minutes, not weeks" response expectations |
| **Solo-dev bandwidth** — custom plugins + verification + moderation + live ops is a lot for one person | Ship the smallest credible product first (hidden+whitelist + per-session names + onboarding), reuse the existing Bun/Elysia + Docker/Dokploy stack, keep plugins modular, automate moderation triage |
| **Drama/harassment spillover** — the rivalries that drive viewership can tip into doxxing/threats | Publish clear, impartially-enforced rules; easy reporting + fast action; design rulesets that channel conflict into bounded, watchable content |
| **Mega-creators self-host** and may never adopt a third-party server | Treat the **mid-tier as the actual business** (large, underserved, monetizable on its own); treat mega-creators as upside via referral/events; offer to *host* their events rather than replace their server |
| **Facepunch TOS/monetization changes** (e.g. the [July 2025 DLC restriction](https://gaminghq.eu/2025/07/18/facepunch-restricts-rust-skins-dlc-community-servers/)) | Commit to compliant revenue lines only (below); diversify into B2B event-infra licensing as a hedge |

## Monetization — deferred / future phase (not active)

**The server is free during the growth phase.** Monetization is documented here only as a
*future* option, to be introduced once population and product-market fit are proven. When/if
introduced, it must stay within
[Facepunch's server monetization rules](https://facepunch.com/legal/servers):

- **Cosmetic + convenience VIP** — queue priority/reserved slot, custom name/tag, sign-image
  upload, owned-skin loadout presets, Discord roles (strictly QoL/cosmetic).
- **"Verified creator membership" subscription** — Facepunch permits charging a fee or
  subscription for server access; framed as bundling protection + reserved slot + priority
  moderation (safety and convenience, not power).
- **Donations / tip jar**, **creator affiliate / revenue-share** (~20% norms),
  **sponsorships**, and later **B2B event-infrastructure licensing**.
- Acquisition flywheel (not direct revenue): lean on Facepunch
  [Twitch/Kick Drops + Support-a-Streamer](https://rust.facepunch.com/support-a-streamer).

**Hard compliance guardrails:** never sell in-game items/kits/resources or gameplay
advantage (= pay-to-win, and forces a Modded listing); **never grant unowned DLC/skins**
(banned since the July 16 2025 TOS update); always label the server unofficial; keep
purchased-DLC access unconditional. Payment processors: Tebex (~5% + VAT) or PayNow (~3.5%).
