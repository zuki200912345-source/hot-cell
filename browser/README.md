# HOT CELL browser practice

A self-contained WebGL practice shift. Open the hosted page and click Enter facility. A keyboard and mouse are required.

You control four suits using keys1–4. A suit stays assigned to a control when you switch to another. E assigns/releases a control; Q reverses its direction. WASD walks, mouse or arrow keys look, F toggles the lamp, T reads the dosimeter, R opens the work order, and Escape pauses.

This is local practice: it has no network peers, voice chat, or hidden traitor assignment. The Unity source in the main HOT CELL repository is the separate LAN multiplayer prototype.

## Local run

Run `npm run dev` with Node22 or later, then open http://localhost:5174/. There are no dependencies to install. Run `npm test` for the actual simulation-rule tests and `npm run build` for the static bundle checks.

The browser saves the shift locally every two seconds and when pausing. The service worker caches the game after a successful load. Saves belong to the browser and origin; a localhost save does not automatically transfer to the hosted page. Clearing site data erases progress. WebGL, pointer capture, browser storage, and offline behavior still need a real-device playtest.

The Sites initializer could not be downloaded because npm connections timed out. This fallback uses plain browser modules and a static Sites bundle.
