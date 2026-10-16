# Stage 1 – Game Analysis and Scope Definition

## 1) Project Intent (Educational Recreation)

This project recreates the **design principles and gameplay feel** of a “Land Builder”-style mobile title in a desktop-native production format.

- **Primary objective:** learn full-cycle game development (analysis → architecture → implementation → shipping).
- **Constraint:** desktop-first controls and UX (mouse + keyboard), while preserving the satisfying incremental island-building loop.
- **Non-goal (for now):** 1:1 legal/content clone of original branded assets.

---

## 2) Core Gameplay Loop (What the player does repeatedly)

At its core, the game loop is an **incremental build cycle**:

1. Player earns/spends a soft currency to purchase terrain expansions (tiles/segments).
2. New land unlocks additional resource opportunities or build points.
3. Player places or upgrades structures that increase production rate.
4. Production yields more currency over time.
5. Player reinvests into further land expansion and upgrades.
6. Milestones/quests unlock new areas, mechanics, or multipliers.

### Loop qualities to preserve

- Immediate visual reward for each purchase.
- Clear short-term goal (“one more tile / one more upgrade”).
- Mid-term progression gates (zones/chapters).
- Long-term compounding growth.

---

## 3) Observable Mechanics (Player-facing)

### 3.1 Terrain Expansion
- Buy adjacent tiles/plots from a revealed frontier.
- Cost scales by region and progression tier.
- Each tile has a type (plain, forest, rocky, water-edge, etc.) that influences what can be built.

### 3.2 Building Placement / Activation
- Certain tiles allow placement of production buildings.
- Buildings may be auto-producing (passive income) or triggered by player actions.
- Placement constraints are explicit (valid/invalid indicators).

### 3.3 Resource/Economy Layer
- Main soft currency drives almost all early/mid game progression.
- Optional secondary resources (e.g., wood/stone/metal equivalents) are used to gate advanced content.

### 3.4 Upgrades
- Upgrade levels increase output efficiency and/or reduce timers.
- Upgrades can be local (per building) and global (economy multipliers).

### 3.5 Objectives and Unlocks
- Quest/task style progression introduces direction.
- Milestones unlock systems progressively to avoid early overload.

### 3.6 Idle/Offline Progress (Optional in MVP, likely in full build)
- Production continues while not actively interacting.
- On return, player receives accumulated rewards with caps.

### 3.7 UI Feedback
- Floating numbers, progress bars, tile highlights, and sound cues reinforce progress.
- Economy changes are always visible and understandable.

---

## 4) Hidden Systems (Under-the-hood design we must reproduce)

### 4.1 Pacing Curves
- Expansion and upgrades use escalating costs, with occasional relief from unlock multipliers.
- Curves should alternate between “push” (slowdown) and “payoff” (new zone/system).

### 4.2 Economy Balance
- Production per minute (PPM) vs. spend per minute (SPM) governs game feel.
- If PPM too high: progression trivializes.
- If PPM too low: stagnation/churn risk.

### 4.3 Friction Design
- Controlled bottlenecks (resource prerequisites, build prerequisites, tile dependency chains).
- Must feel fair and telegraphed.

### 4.4 Reward Cadence
- Micro reward: every click/place/purchase.
- Meso reward: every objective chain completion.
- Macro reward: zone completion and major unlock.

### 4.5 Session Design (Desktop adaptation)
- Mobile loop supports very short sessions.
- Desktop target should support both:
  - Short “check-in” loops (3–10 minutes), and
  - Longer optimization sessions (20–60 minutes).

---

## 5) Progression Structure Blueprint

### 5.1 Progression Layers

1. **Tile progression:** unlock map coverage.
2. **Building progression:** unlock building types and higher tiers.
3. **Economy progression:** unlock multipliers, automation, and conversion chains.
4. **Meta progression (later):** global unlock trees, prestige/rebirth, or chapter bonuses.

### 5.2 Gating Model

- **Hard gates:** required objective completion to unlock next region/system.
- **Soft gates:** high costs that encourage optimization before advancing.

### 5.3 Win/Completion Conditions

For the recreated baseline game:
- Complete all tiles in current world/chapter.
- Reach target economy threshold.
- Complete core objective chain.

Later expansions can add endless/postgame systems.

---

## 6) Player Goals by Horizon

### Immediate (seconds to minutes)
- Afford next tile.
- Place next building.
- Complete current objective step.

### Short-term (5–20 minutes)
- Unlock new building type.
- Stabilize positive income cycle.
- Reach zone checkpoint.

### Mid-term (30–120 minutes cumulative)
- Complete a region.
- Build a high-efficiency production layout.
- Unlock next major mechanic.

### Long-term (multi-session)
- Master economy optimization.
- Complete all designed regions.
- Push optional post-completion targets.

---

## 7) Feedback Systems (Required for “feel”)

1. **Visual state feedback**
   - Hover/selection/highlight states for tiles and buildings.
   - Build previews with valid/invalid tinting.
2. **Economic feedback**
   - Real-time currency counters.
   - Source attribution (where income comes from).
3. **Progress feedback**
   - Objective tracker with clear next actions.
   - Unlock banners and tooltips.
4. **Reward feedback**
   - Particle/pop effects for purchases and completions.
   - Audio stingers for milestone events.

---

## 8) Feature Classification (Core / Secondary / Polish)

## 8.1 Core Systems (must exist for first true game identity)

1. Tile-based map with expansion purchases.
2. Currency economy with production and spending.
3. Building placement and upgrade system.
4. Objective/milestone progression chain.
5. Save/load persistence.
6. Core HUD (currency, objective, build controls).
7. Basic feedback (highlighting, popups, minimal SFX hooks).

## 8.2 Secondary Systems (important after core stability)

1. Multiple resource types and conversion chains.
2. Offline/idle reward accumulation.
3. Multiple regions/biomes with unique rules.
4. Global upgrade tree.
5. Enhanced analytics/debug balancing overlays.

## 8.3 Polish Features (production quality, can be deferred)

1. Rich animation blending and advanced VFX.
2. Dynamic music layers and full soundscape.
3. Accessibility options (scaling, colorblind modes, remapping).
4. Tutorial narrative framing and localization pipeline.
5. Achievement system and endgame/postgame loops.

---

## 9) Full Feature Inventory (Stage 1 output checklist)

### 9.1 World & Map
- Grid/graph map representation.
- Tile ownership state (locked, unlockable, unlocked).
- Tile metadata (terrain type, build slots, adjacency links).

### 9.2 Economy
- Currency definitions (soft + optional materials).
- Income generation model (per second tick).
- Cost formula framework (scalable curves).
- Spend events and transaction logging hooks.

### 9.3 Buildings
- Building catalog and unlock conditions.
- Placement validation rules.
- Production formulas per building level.
- Upgrade path definitions.

### 9.4 Progression
- Objective data model (steps, rewards, requirements).
- Unlock manager (systems, buildings, regions).
- Milestone reward distribution.

### 9.5 UI/UX
- HUD for economy/progression.
- Build mode interactions.
- Information panels for tiles/buildings.
- Notification feed/toasts.

### 9.6 Persistence
- Save slots/profile support.
- Versioned save schema.
- Autosave + manual save triggers.

### 9.7 Telemetry/Debug (internal)
- Economy balance overlay.
- Time acceleration test mode.
- Spawn/resource debug commands.

### 9.8 Content Pipeline
- Data-driven config files for tiles, buildings, upgrades.
- Validation scripts for config integrity.
- Separation of code from design data.

---

## 10) Risks and Early Mitigations

1. **Risk:** economy curve feels grindy or trivial.
   - **Mitigation:** spreadsheet-driven balancing and in-game debug graphing from start.
2. **Risk:** feature creep before playable core.
   - **Mitigation:** lock MVP scope to Core Systems list.
3. **Risk:** weak desktop UX if copied directly from mobile.
   - **Mitigation:** redesign interaction for mouse/keyboard with fewer taps and richer hover info.
4. **Risk:** save incompatibility during iteration.
   - **Mitigation:** versioned schema + migration layer policy.

---

## 11) Stage-Gate Exit Criteria (Stage 1 complete only when true)

Stage 1 is complete when:

- Core loop is explicitly defined.
- Player-facing and hidden systems are documented.
- Full feature list is categorized into Core/Secondary/Polish.
- Progression model and player goals are defined across time horizons.
- Risks and mitigation strategies are identified.

**Status:** Complete. Ready to proceed to Stage 2 (System Architecture Design) in the next step.
