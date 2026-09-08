# HOT CELL

A cooperative nuclear decommissioning game. Four to six identical suits share physical procedures, private dosimeters and uncertain intentions.

**Play the browser practice build:** https://hot-cell-practice.zuki2009.chatgpt.site

The hosted practice build is private to its owner's ChatGPT account. Browser source is in `browser/` and can also run locally without dependencies.

## Current builds

| Build | Status |
| --- | --- |
| Browser practice | Self-contained WebGL; one person switches among four suits. Crane, bolt order, shared carrying, radiation, lockout and report. Browser saves and offline caching included. No online peers, voice chat or traitor assignment in this practice mode. |
| Unity multiplayer source | Unity 6 grey-box room, NGO LAN networking, host physics, private role/dose snapshots, proximity voice, procedures and shift report. Editor import, compilation, standalone build and multiplayer playtest are still unverified. |

The supplied [build brief](docs/BUILD_BRIEF.md) is unchanged. Neither prototype is the finished Steam game.

## Run browser practice

With Node 22 or later:

```sh
cd browser
npm run dev
```

Open http://localhost:5174/. Click Enter facility. Use WASD and the mouse; E operates, Q reverses, F toggles the lamp, T reads the wrist, R opens the work order, and 1–4 switches suits. A suit remains at its assigned control when you switch. Escape pauses. A keyboard and mouse are required.

The browser stores progress locally. Saves do not transfer between the localhost and hosted addresses. After a successful first load, its service worker caches the game for offline use; verify this in the browser before relying on it offline.

## Open Unity

1. Open this repository root with Unity **6000.0.57f1**. Let the pinned packages resolve.
2. Select **HOT CELL / Generate Prototype**, then open `Assets/HotCell/Scenes/Prototype.unity`.
3. Press Play to host. Use **HOT CELL / Build** to produce a desktop player with the appropriate Unity build module installed.
4. Launch other players with `-hotcell-join HOST_IP -hotcell-port 7777`. Four to six players are required to start at the physical shift key.

See [PLAYTEST.md](docs/PLAYTEST.md) for controls, setup and remaining checks. [ARCHITECTURE.md](docs/ARCHITECTURE.md) describes the implemented networking and privacy boundaries. Steam transport, lobbies and Steam Voice are not integrated yet.

## Verified checks

- 26 core C# checks pass against the production rules.
- 12 browser simulation checks pass.
- Roslyn parses the Unity source as C# 9 without syntax errors. This does not establish Unity compilation or runtime behavior.
- Static browser build checks pass. A full interactive playthrough remains to be performed.

```sh
dotnet run --project Tests/Core/HotCell.Core.Tests.csproj
cd browser
npm test
npm run build
```
