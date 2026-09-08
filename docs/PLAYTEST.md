# Prototype playtest

This is a source prototype. Unity import, compilation, standalone builds and multiplayer play have not yet been verified. Complete the technical checks below before using a session to judge the game. The original [build brief](BUILD_BRIEF.md) remains unchanged.

## Prepare and connect

1. Open the project in **Unity 6000.0.57f1** and let its packages resolve. Netcode for GameObjects is pinned to **2.7.0**.
2. Use **HOT CELL → Generate Prototype**, then open `Assets/HotCell/Scenes/Prototype.unity`. The editor also attempts generation on first import. Pressing Play starts a host.
3. Create standalone players using **HOT CELL → Build → Windows / macOS / Linux**. Install the corresponding Unity build support module first.
4. Use four to six players on the same trusted LAN, including the host. The host listens on **UDP 7777** by default; allow the player through its local firewall for that LAN.

Pass these arguments to the built player executable:

| Mode | Arguments |
| --- | --- |
| Host | None, or `-hotcell-port 7777` |
| Join | `-hotcell-join 192.168.1.20 -hotcell-port 7777` |

Replace the example address with the host's LAN address. Both sides must use the same port. On macOS, arguments can be supplied with `open -n "Builds/macOS/HOT CELL.app" --args -hotcell-join 192.168.1.20 -hotcell-port 7777`. There is no lobby browser, Steam connection or relay service.

Once at least four suits are connected, the host looks at the physical **SHIFT START** key in the ready room and holds **E**. Six is the maximum. Joining is blocked during an active shift. The host can use the same key after the report to reset the work.

## Controls

| Input | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Hold E | Operate the object being looked at, within reach |
| Hold Q + E | Reverse a control, lower the hoist or loosen a bolt |
| F | Toggle suit lamp |
| R | Show or put away the work order and private contract |
| Hold T | Raise your wrist dosimeter |
| Hold V | Proximity voice; microphone permission is requested on first use |
| 1–6 | Select a suit for the physical lockout lever |
| Escape / left click | Free the mouse / lock it again |

Put away the work order and lower your wrist before operating equipment. To inspect another suit's dose, both players stay still; look at that suit nearby and hold **E** for two seconds.

## One shift

The timer is 20 minutes and the dose ceiling is 100 prototype units. Read the work order after starting to learn your own contract. Some shifts have no traitor; everyone uses the same controls.

- Open the shield door. Put operators at the hoist and traverse panels, with a spotter near the pool. Both operators must hold their controls for the crane to move. Traverse fully into alignment before lowering into the cask; an early lowering can cause an immediate breach.
- Keep one player holding the lid while another tightens the eight bolts to **80 ± 5 Nm**, in order **1, 5, 3, 7, 2, 6, 4, 8**. Only the active wrench operator receives the torque reading. Back off all eight bolts to zero to reset an invalid sequence.
- Two players hold the cask's carrying handles and move it through the door to the marked transport bay. A separate movable shield requires one handler.
- **Assemble in transport or ready room** before dispatch. At least three suits must have evacuated alive below the ceiling. The dispatch handle ends the shift and reveals the report, including final doses and roles.
- The lockout lever takes six continuous seconds and sounds an alarm. It ejects the selected other suit to observation. There are two successful uses per shift; releasing or changing the selection cancels the hold.

## Technical checks still required

- [ ] Import and compile in the pinned Unity editor; build and launch each intended desktop platform.
- [ ] Connect four players, then six. Verify start restrictions, blocked late joins, disconnect handling and a second shift after reset.
- [ ] Test microphone permission granted and denied, missing microphone, V release, focus loss and quitting. Listen for distance falloff, facing changes, concrete/door muffling, no own-voice loopback and acceptable delay under packet loss.
- [ ] Complete a sealed delivery with three survivors; separately verify an incorrect crane lowering, wrong bolt order, retry, unsealed dispatch, timer expiry and collapse.
- [ ] Verify carrying through the doorway, dropped objects, movable shielding and client movement correction on all peers.
- [ ] Check private dose/role visibility, two-second inspection, cancelled/completed lockouts and final report results.
- [ ] Listen to geiger changes and ventilation stopping; inspect wrist/work-order readability and visor symptoms at 25%, 50%, 75% and collapse.

## The social test

Run one 20-minute session with four friends using **the game's proximity voice**. An external group call would bypass the muffling and distance constraints being tested. Avoid explaining suspected mistakes while the shift is running.

After the report, ask each player separately:

- Did another player's mistake seem deliberate? What made you think that?
- Did you knowingly accept a dangerous procedure to save time?
- Did the role reveal change your account of what happened?

Keep notes on the incident, who could see or hear it, and whether confusion came from another player or a technical fault. Fix faults before drawing conclusions about the social design.
