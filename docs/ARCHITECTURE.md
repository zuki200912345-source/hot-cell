# Prototype architecture

The repository contains a Unity source prototype targeting **6000.0.57f1**, the built-in render pipeline and **Netcode for GameObjects 2.7.0**. It has not yet been compiled or run in Unity. The original [build brief](BUILD_BRIEF.md) is preserved; this document describes the current implementation rather than the complete proposed game.

## Components

| Component | Responsibility |
| --- | --- |
| `HotCellBootstrap` | Session startup, input, host simulation, shifts, role assignment, private snapshots, hand displays and reports |
| `Facility` | Runtime greybox geometry, work targets, bodies, signs, lights and raycast-based shielding |
| `CrewRig` | Suit geometry, character-controller movement, local camera attachment and remote interpolation |
| `SessionLink` / `Protocol` | NGO custom messages, payload checks and serializable input/state records |
| `VoiceAudio` | Microphone capture, spatial PCM8 playback, mask/wall filtering and synthesized environmental sounds |
| `VisorEffect` / `Resources/Visor.shader` | Local dose symptoms: subtle noise and later desaturation; plain blit fallback |
| `Core` | Unity-independent radiation, torque sequence, role/outcome and lockout rules |
| `Editor/PrototypeProject` | Scene generation and desktop build menu commands |

Every peer constructs the same facility locally. The host alone simulates cask/shield rigidbodies, radiation, procedures, lockout, role assignment and the final result. Client copies of those bodies are kinematic and follow snapshots. Carrying applies bounded forces toward handlers' combined target position; crane travel and hoist height follow a scripted shared procedure.

## Simulation and transport

The host advances at **20 Hz**. Inputs also target 20 Hz, contain a monotonic sequence number and describe controls rather than completed actions. The host rejects stale/non-finite input, clamps controls, checks interaction reach, and clears held movement/use after 250 ms without fresh input. Sending more packets does not advance simulation time faster.

Clients predict their own character movement immediately and apply soft positional correction toward authoritative snapshots. Errors over two metres cause a teleport. Remote suits and carried bodies interpolate toward host state. This is a basic correction scheme, without acknowledged-input replay or rollback.

UnityTransport uses direct LAN UDP, default port **7777**. Launching without `-hotcell-join` hosts; `-hotcell-join IP` connects, and `-hotcell-port PORT` changes the port.

| Message | Delivery | Contents |
| --- | --- | --- |
| `hc.input.1` | Unreliable sequenced | Movement, view and held controls |
| `hc.state.1` | Reliable fragmented sequenced | Recipient-specific JSON snapshot, emitted every host tick |
| `hc.voice.1` | Unreliable | Client's 400-byte voice chunk sent to the host |
| `hc.sound.1` | Unreliable | Host-relayed slot number and voice chunk |

Snapshots are capped at 32 KiB. Reliable full JSON state is convenient for this prototype but can accumulate latency under congestion; it is not a finished high-latency replication design. Host-local input, state and voice use the same callbacks without a network round trip.

## Private state and trust

Each recipient gets its own dose, dose rate and role. Public suit records contain position/yaw, connection, lamp, collapse and ejection state. Other exact doses appear only after a valid nearby inspection or in the final report. The torque reading goes only to the current wrench operator. The host constructs a separate snapshot for each player.

This is a **trusted LAN prototype**: admission checks the shift phase and six-player limit, with no player identity authentication. The player hosting the session holds every secret and controls the simulation. Payload validation is not an assurance against hostile hosts or arbitrary internet clients. There is no Steam lobby, relay, Steam authentication, Steam Voice integration or host migration.

## Audio and presentation

V captures microphone audio as **8 kHz mono unsigned PCM8**, in 400-sample/50 ms chunks. The host identifies the sender, limits forwarding rate and relays only to peers within 18 metres. Receivers use bounded buffers, spatial sources, facing/distance attenuation and a stronger low-pass filter through shielding. This temporary transport has no codec, packet-loss concealment or production voice-quality validation.

Suit fan, breathing, geiger clicks, ventilation and the alarm are synthesized at startup. Geiger timing follows instantaneous local dose rate. The camera effect adds restrained noise above 25% cumulative dose and desaturation above 75%. Microphone capture stops on release, loss of eligibility/focus, disable or destruction; audio callbacks operate only on locked managed buffers.

The layout, suits and hand displays use primitive geometry. The current source does not implement the brief's complete decontamination process, number-plate removal, inventory/tool handling, full art pass or Steam services. Dose and mechanical parameters are game tuning values, not radiation safety guidance.

## Verification boundary

`dotnet run --project Tests/Core/HotCell.Core.Tests.csproj` runs the standalone **.NET 8** rules checks. These do not exercise Unity physics, rendering, NGO, microphone permissions or gameplay. Roslyn syntax checks likewise do not establish Unity compilation. Follow [PLAYTEST.md](PLAYTEST.md) for the remaining editor, build, network and four-player verification.
