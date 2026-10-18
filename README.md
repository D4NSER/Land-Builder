# Land Builder

A cozy hex tile-laying game in the spirit of *Dorfromantik*, *Islanders* and SayGames' *Land Builder*. Draw tiles from your stack, match their edges to grow an island, chase quests and keep the stack alive for as long as you can.

Built with **Godot 4.6 (.NET)** and C#. Every mesh, shader effect, icon and sound is generated in code, so there are no art or audio assets to manage.

![Gameplay](docs/screenshots/gameplay.jpg)

| Title | Late in a run (Amber Vale) |
| --- | --- |
| ![Title screen](docs/screenshots/title.jpg) | ![Late game](docs/screenshots/late-game.jpg) |

![Architecture per theme](docs/screenshots/architecture.jpg)

![Seasons: spring, summer, autumn, winter](docs/screenshots/seasons.jpg)

![Wonders: castle, cathedral, lighthouse, rocket pad, great pagoda, observatory, harbour](docs/screenshots/wonders.jpg)

## Playing

| Action | Input |
| --- | --- |
| Place tile | Left click on a glowing outline |
| Rotate | `Q` / `E`, right-click, or `Shift` + mouse wheel |
| Pan | Left-drag, middle-drag or `WASD` / arrow keys |
| Orbit | Right-drag |
| Zoom | Mouse wheel |
| Re-centre | `F` |
| Hint | `H` or `Space` |
| Undo (3 per island) | `Ctrl+Z` or `Backspace` |
| Pick up / put back a wonder card | Click the card or `G` (`Esc` cancels) |
| Pause | `Esc` |
| Fullscreen | `F11` |

### Rules and scoring
- **Plan ahead:** you see the tile in hand plus the next two, and play them in order. Every tile is dealt with at least two spots where all its touching edges match (if a draw would fit nowhere cleanly, it is reshaped to fit a real gap), so there is always a right move to look for.
- **Edges:** each tile has six wedges of terrain: meadow, forest, wheat field, village or lake to start. Every edge that touches the same terrain scores **10 points**. Mismatches are allowed but score nothing.
- **World themes:** every island rolls one of six themes. Each has its own architecture, tree species, palette, sea and sky. It also shifts which terrains show up and decides which new terrains milestones unlock: Mountains, Dunes, Snowfields, Marsh or Orchards.

  | Theme | Architecture | Trees |
  | --- | --- | --- |
  | Green Isles | European half-timbered houses, townhouses, churches, windmills | pine, fir, oak, birch, maple, poplar |
  | Northern Reach | Falu-red cabins with turf roofs, stave churches | fir, pine, birch |
  | Sakura Shores | Minka houses, pagodas, torii gates, stone lanterns, rice paddies | cherry, black pine, bamboo, maple, cypress |
  | Coral Coast | Thatched stilt huts, longhouses, canoes; rainforest | jungle giants, palms, bananas, ferns |
  | Sunlit Plains | Rondavels, kraals, granaries | acacia, baobab |
  | Amber Vale | Rustic farmsteads, red barns, silos | maple, oak, pine |
- **Growing regions:** connected land levels up as it grows, and every rung pays points (and, from the second rung, tiles). A village region becomes a Village, Town, City, Metropolis and finally a Capital; forests go from Grove to Wildwood, lakes from Pond to Sea of Isles, and so on for every terrain except meadow. Merging two regions pays only for the new rungs. The **Your land** panel shows your biggest regions and how far each one is from its next rung.
- **Completing regions:** fully enclosing a region of six or more wedges pays 5 points per wedge, plus tiles for big ones.
- **Wonders:** every second milestone and every third completed quest earns a wonder card: Castle, Cathedral, Great Pagoda, Harbour, Lighthouse, Rocket Pad, Observatory, Botanical Park, Tall Ship or Sequoia Park. The theme favours some of them. Each wonder covers 4 to 6 tiles, spreading out from a central plot. Click the card (or press **G**) to pick it up, rotate the blueprint, and lay it over the island or just off its shore. It doesn't use your stack. Every golden plot needs a tile with enough of the right terrain: the Tall Ship's six plots each need a tile with a water side, the Castle's need town and mountain. Plots can go over tiles that already suit them, so a well-built island can raise a wonder at once. Each card shows a little map of its footprint, empty plots stand on survey stakes and glow green when the tile in your hand would fit, and while a blueprint waits the stack deals tiles that suit it more often. Filled plots get scaffolding and cranes. When the last plot is filled, the landmark rises over the whole footprint for big points and extra tiles. A finished wonder grows a district: building next to it pays a +20 bonus, and its style spills onto neighbouring tiles (redwoods spread into nearby forest, banner towers rise in nearby towns, boats crowd nearby lakes). Castles get curtain walls and towers across their plots, harbours get piers, the Tall Ship rides in a basin dug for it, rockets launch every half minute and lighthouse beams sweep.
- **Growing structures:** settlements are built to match their rung. Towns get taller houses, market squares and lamp-lit streets, Cities get apartment blocks, Metropolises get glass towers (square, round or stepped) clustered downtown with parks and a stadium, and a Capital gets one great landmark at its heart in the island's own style: a gilded spire, a castle keep, a copper-roofed clock tower, a domed terrace tower or a mud-brick tower. Roofs follow the local architecture too. Settlements are packed with homes, with no roads cutting through them. Forests grow in stands of mixed species with saplings, old giants and small glades, and each biome has its own clearly distinct ground colour. Meadows get grazing sheep and cows. Lakes rise into sandy shores where they stop and flow on where they meet another lake; rivers run narrow through low meadow banks. Mountain ridges continue across matched mountain edges, and big forests get ancient giant trees. Houses are built in place rather than popping in: walls rise, roofs drop on, chimneys smoke, and cherry trees shed petals.
- **Seasons:** each milestone turns the season. Spring brings blossom, autumn turns the woods red and gold, and winter lays snow on roofs, trees and ground and freezes the lakes. Islands without winters get a dry or rainy season instead. Weather follows along: petals, pollen and fireflies, falling leaves, snowfall.
- **Rivers:** unlocked at the second milestone on every island. River edges must meet another river or flow into a lake; a tile that can't legally go anywhere is set aside so you're never stuck.
- **Perfect / Flawless:** touch 3+ tiles with every edge matching for a *Perfect* (bonus points + 1 tile). Fill a hole with all six edges matching for a *Flawless* (+100, +2 tiles).
- **Streak:** clean fits against two or more tiles raise a multiplier (up to ×2.0). A mismatched edge resets it.
- **Quests:** pins appear over forests, fields, villages and lakes. Grow the area to the target (or *exactly* to it) for points and extra tiles. A quest fails if its area gets closed in or overshoots an exact target.
- **Milestones:** score thresholds award tiles and unlock the theme's terrains and rivers.
- **End of run:** the island is complete when the stack runs out. Runs autosave after every move, and **Continue** resumes them. Finished runs go on a local leaderboard.

All tuning numbers live in [`GameRules.cs`](src/LandBuilder.Core/Domain/GameRules.cs). A bot-driven balance test keeps the pacing in band.

## Running

```powershell
.\Start-LandBuilder.ps1            # build, run tests, launch the game
.\Start-LandBuilder.ps1 -SkipTests # just build and launch
.\Start-LandBuilder.ps1 -Editor    # open in the Godot editor
```

The script finds Godot through `GODOT_BIN`, `PATH`, or a Godot 4.6 .NET build in Downloads, Desktop or Program Files.

Alternatively, open the folder in Godot 4.6 .NET and press Play (`res://scenes/Main.tscn`).

## Testing

```powershell
dotnet test .\LandBuilder.sln
```

Rules, determinism, persistence and a balance simulation (a greedy bot plays full runs) are covered.

In-engine smoke test, which drives real mouse and keyboard events through the game and exits non-zero on failure:

```powershell
<godot-console-exe> --path . -- --selftest
```

Other debug flags (after `--`):
- `--autoplay`: a bot plays continuously. Add `--theme=Nordic` (or any theme) to force a theme.
- `--title`: shows the attract-mode title screen.
- `--capture-dir=<dir> --capture-every=<frames>`: saves periodic screenshots and logs FPS.
- `--zoom-water-at=<frame> --zoom-terrain=<Terrain>`: freezes the bot and zooms in on a terrain.
- `--lab [--lab-terrains | --lab-showcase | --lab-lakes | --lab-city [--lab-town] | --lab-wonders [--lab-sites] [--lab-focus=<n>] | --lab-bench=<tiles>] [--lab-close] [--lab-far] [--theme=<Theme>] [--season=<Season>] [--lab-hide=water,props,shelf] [--lab-off=ssao,dof,glow,shadows]`: a fixed tile patch for isolating rendering issues and reviewing themes and seasons.

Automated runs write to `user://automation/` and never touch your real save or leaderboard.

## Code layout

```
src/LandBuilder.Core/          Pure C# rules, no Godot dependency
  Domain/GameEngine.cs         Placement evaluation, scoring, quests, milestones, hint bot
  Domain/RegionMap.cs          Union-find over tile wedges for connected terrain areas
  Domain/TileGenerator.cs      Contiguous-segment tile generation with terrain affinities and river overlays
  Domain/WorldTheme.cs         Island themes: terrain weights and milestone unlock order
  Domain/Wonders.cs            Wonder footprints, plot rules, blueprint planning and progress
  Domain/RegionTiers.cs        Region growth ladders (Village → Capital...) and completion rewards
  Application/GameSession.cs   Current run, undo history, event fan-out
  Infrastructure/              Save (schema 5) and leaderboard persistence
src/Presentation/              Godot layer
  Main.cs                      Flow, input, hand tile, event-to-feedback choreography
  BoardView.cs / TileVisual.cs Tile nodes, spring-driven drop/squash/ripple, affordances
  TerrainSampler.cs            Blended per-wedge height and colour (world-space noise)
  TileMeshBuilder.cs           Ground, skirt and lake meshes
  PropBuilder.cs               Trees, houses, windmills, boats... merged per tile
  ThemeStyle.cs                Per-theme palettes, tree species, architecture, sea, sky and seasons
  Seasons.cs                   Season director feeding the season_* global shader parameters
  WonderModels*.cs             Landmarks, scaffolding, the Tall Ship and the surroundings spanning each plot
  WonderLayout.cs              Where a wonder stands: centre, scale, facing, and the ground it levels or floods
  BoardView.Sites.cs           Blueprint outlines, plot scaffolding, finished wonders and the placement ghost
  PropBuilder.City*.cs         Apartment blocks, towers, parks, stadiums, themed Capital landmarks, livestock
  FootprintIcon.cs             The footprint map drawn on wonder cards
  Hud.Land.cs                  The "Your land" panel
  TileContext.cs               Per-tile neighbourhood (town tier, roads, ridges) for structures that span tiles
  PropBuilder*.cs              Props: core scatter, tree library, architecture sets, nature
  WorldView.cs                 Sky shader, sun and day progression, grading, ocean, seabed, clouds, birds, motes
  CameraRig.cs                 Eased orbit camera, auto-framing, shake, tilt-shift DOF
  Hud.cs / Menus.cs / UiKit.cs In-game HUD, menus and the shared UI theme
  Audio.cs                     Synthesised SFX, ambience and generative music
shaders/                       terrain, props (pop-in + wind), water (depth foam, murk), sky (clouds), vignette
tests/LandBuilder.Tests/       xUnit tests for the Core
```

Earlier milestone design notes are kept in [`docs/history/`](docs/history/) for reference. The current rules are the ones described above.
