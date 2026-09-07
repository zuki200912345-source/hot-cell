# HOT CELL — Build Brief

**Working title:** HOT CELL (a real term: the shielded chamber where radioactive material is handled remotely). Alternates: SIEVERT, DOSE, COLD SHUTDOWN.

**Format:** 4–6 player online co-op, first person, Steam, $12–15.
**Session length:** 20–35 minutes per shift.
**Team assumption:** 1–3 people. Grey-box prototype in two weeks.

---

## 1. The concept in one paragraph

You are a contract decommissioning crew dismantling a dead nuclear facility. You wear identical sealed suits. You cannot see each other's faces, your voices are muffled through the mask, and you are identified only by a stencilled number on your chest. The job is physical: cut, lift, carry, seal, and transport things that will kill you if handled wrong. Radiation is invisible and cumulative, and your dose is private to you. One member of the crew may be being paid to make the job fail. They have no special abilities, no sabotage button, and no way to kill you directly. All they can do is make mistakes on purpose. And sometimes — you are never told how often — there is no traitor at all, and the shift just went badly.

---

## 2. Design thesis

**The Deniability Engine.** This is the single organising principle. Everything else in this document serves it.

> No action available to the traitor is unavailable to an innocent crew member. There are no traitor-only verbs.

The traitor's entire advantage is **information and intent**. They know which cask is mislabelled because they mislabelled it. They know the shield door is open because they opened it and then walked away casually. An innocent player who is simply bad at the game produces an identical event stream.

This is why the game works and why it is not Among Us with hands. In Among Us, sabotage is a distinct act that only the impostor can perform, so the entire game is about alibi and location. Here, the ambiguity is native to the physics. The question is never "who was in electrical," it is **"was that malice or was that Omar."**

**Corollary rules that protect the engine:**

- No kill button. No takedown animation. The traitor never directly harms anyone. They engineer conditions.
- No task bar, no completion percentage, no visible progress meter that could act as a lie detector.
- No meetings. No voting screen. No round pause. Accusation happens live, over voice, while your hands are full and the thing is still dangerous. If you want to argue, you have to put down what you are carrying, and that has a cost.
- Randomise traitor count per shift: roughly 25% of shifts have zero traitors, 65% have one, 10% have two. Never announced. Revealed only in the post-shift report.

---

## 3. Core loop

1. **Briefing.** The crew reads a physical work order on a clipboard in the ready room. It states the objective, the shift timer, and the dose ceiling. Roles are picked by whoever grabs which tool.
2. **Entry.** Suit up, clip on dosimeter, pass through the airlock. Once inside, you cannot leave without decontamination, which takes 40 seconds and locks you in a glass chamber where everyone can see you standing still and doing nothing.
3. **The work.** Execute a multi-stage physical procedure. Every stage requires at least two people and produces natural information asymmetry.
4. **Degradation.** Things go wrong. Some of it is you. Some of it may not be.
5. **Extraction.** Get the sealed object to the transport bay before the shift timer expires.
6. **Report.** Post-shift screen shows: containment status, each crew member's final dose, and whether there was a traitor. This reveal is the emotional payload of the entire game.

---

## 4. Systems

### 4.1 Radiation

The antagonist of the game is an invisible number.

- Point sources emit dose rate falling off with inverse square distance.
- Shielding volumes (concrete walls, lead glass, the shielded cart, closed blast doors) attenuate via a simple raycast against a shield layer with per-material attenuation values.
- Each player accumulates **absorbed dose** per tick. Dose never decreases.
- Dose is **private**. You see only your own. To read someone else's you must physically stand next to them and grab their wrist dosimeter, which takes two seconds and requires them to hold still.

Dose thresholds produce escalating, purely diegetic effects:

| Dose | Effect |
|---|---|
| 0–25% | Nothing. Faster geiger clicking. |
| 25–50% | Occasional visual snow, mild input latency. |
| 50–75% | Hand tremor: carried objects wobble and grip strength drops. Nausea audio. |
| 75–95% | Vision desaturates toward monochrome. Grip fails intermittently. You are now a liability and everyone can see you fumbling. |
| 95–100% | Collapse. You are dragged out or you die in the room. |

**Why the tremor mechanic matters:** an irradiated innocent becomes visibly, mechanically incompetent. This is the deniability engine turned up to maximum. A crew member who is genuinely dying looks exactly like a saboteur, and a saboteur can deliberately dose themselves lightly to acquire an excuse.

### 4.2 Suits and identity

- All suits are identical. Chest stencil number only (01 through 06), readable at close range in good light.
- Voice is proximity-based, low-pass filtered to simulate the mask, and directional. Distance and concrete walls degrade it hard.
- A suit's number plate can be removed with the same tool used for cutting. This is a legitimate action (plates get contaminated and are replaced) and an obvious identity attack. It should feel dangerous to do.
- Suit lamps are directional and are the primary light source. Turning yours off is legitimate (glare on lead glass) and sinister.

### 4.3 Objects and procedures

The physical layer must be **slow, heavy, and two-handed**. Nothing snaps into place. Nothing auto-completes.

Sample procedure — Fuel Cask Sealing:
1. Two players operate an overhead crane to lift a fuel assembly from the pool. Crane has separate hoist and traverse controls that must be operated by two different people at two different panels, neither of whom can see the pool clearly.
2. A third player at the pool edge calls out alignment over voice. **This player is the only one who can see the truth.**
3. Assembly is lowered into a transport cask.
4. The cask lid requires a torque sequence: eight bolts, in a specific star pattern, to a specific value shown on a gauge only the person holding the wrench can read.
5. The sealed cask is winched onto a cart and pushed to the transport bay by three people, because it is heavier than any two.

Note how many single points of private information that procedure contains. Every one is a place a traitor can lie and an innocent can simply be wrong.

### 4.4 Sabotage vectors (all of which are legitimate actions)

- Leave a shield door open behind you. Everyone forgets doors.
- Call out a wrong crane alignment. You were looking at glare.
- Skip a bolt in the torque sequence. Miscounted.
- Swap two labelled containers. They look the same.
- Move the shielded cart away from where someone is kneeling. You needed it.
- Switch off a ventilation fan. It was loud and you were trying to hear someone.
- Report a gauge reading that was never true. You are the only person who saw it.
- Drop something. The most common event in the game.

Build the tool list so that **every tool has a destructive misuse and a necessary use.** If you ever find yourself adding a verb that only a traitor would want, delete it.

### 4.5 Accusation and lockout

There is no vote. There is a **lockout lever** at the airlock.

- Any player can pull it to eject one crew member by number. It takes six seconds of holding, during which an alarm sounds facility-wide and the lever's location is obvious.
- Ejection is not death. The ejected player watches from the observation window and can still speak, but muffled through glass and unable to touch anything.
- **The cost is mechanical, not social.** Every procedure needs hands. Eject an innocent and the remaining crew may physically be unable to lift the cask. Paranoia is punished by the weight of objects, not by a scolding.
- Limit: two lockouts per shift.

### 4.6 Win and lose

**Crew wins** if the object is sealed, transported, and at least three crew members finish under the dose ceiling.

**Traitor wins** on containment breach, shift timer expiry, or crew incapacitation — **and only if the traitor personally finishes under the dose ceiling.** They cannot martyr themselves. This forces subtlety and stops the degenerate strategy of running into the hot zone and flooding the room.

**Nobody wins** on a mutual failure, and the report says so, which is funnier and more interesting than a win screen.

---

## 5. UI and UX direction

**Hard rule: the interface is diegetic or it does not exist.**

- **No floating HUD.** No health bar, no stamina bar, no objective tracker, no minimap, no waypoint markers, no interaction prompts hovering over objects.
- **Dose** is read by raising your left wrist, which physically occupies your hand and blocks part of your view. The dosimeter is a small backlit LCD with a four-digit readout and a dying backlight.
- **Objectives** live on a clipboard. Carrying the clipboard means not carrying something else. Someone has to be the person holding the clipboard and that person is doing less work.
- **Health** is communicated through vision, grip, and breathing. Never a number.
- **Interaction** is communicated by the object, not by text. A grabbable object has worn paint on its handle. A valve that turns has a wear ring around it. Teach affordance through material, not through a tooltip.
- **Suit visor** may show at most three things, rendered as if physically etched or projected on the inside of glass, subject to fogging from your breath and glare from lamps: air supply, radio channel, suit integrity. Nothing else. Ever.

**Typography:** industrial stencil and condensed grotesque. Think DIN 1451, Soviet GOST plate lettering, and machine-stamped serials. Never a rounded geometric sans. Never anything that looks like a mobile app.

**Palette:** desaturated concrete, oxidised steel, sodium-vapour amber. Exactly two saturated colours exist in the entire game — hazard yellow and alarm red — and both mean danger. If a colour appears, it is telling you something is wrong. Everything else is grey, rust, and dirty white.

**Menus:** the main menu is a physical operations binder on a desk in a site office. You flip pages. Settings are a clipboard form. Server browser is a job board with pinned paper. No animated backgrounds, no parallax, no gradient panels, no card layouts with rounded corners and drop shadows.

**Audio is the primary UI.** This is not decoration, it is the interface layer:
- Geiger click rate is your dose rate and must be legible enough that experienced players navigate by ear.
- Your own suit fan and breathing are always present and change with exertion and dose.
- Voice muffling and occlusion must be tuned aggressively enough that walking around a corner mid-sentence loses you information.
- Silence is a tool. When the ventilation dies, the absence should be alarming.

---

## 6. Art direction

Low polygon counts are fine and desirable. **Material accuracy is not optional.** The look is not "stylised" and it is not sci-fi. Reference brutalist infrastructure, decommissioned Soviet and British industrial plant, and the specific visual vocabulary of real radiological work: yellow-and-magenta trefoil signage, rope barriers, chalk markings on floors, laminated procedure cards zip-tied to railings, mismatched replacement parts.

Everything is worn, patched, and slightly wrong. The facility should feel like it was built by people who are now dead.

**Anti-references:** anything that looks like a Unity asset store horror pack. Anything with neon. Anything with a hexagonal UI motif.

---

## 7. Technical architecture

**Engine:** Unity 6. Godot 4 is viable but Unity's physics maturity and Steamworks integration will save weeks on exactly the part that is hardest here.

**Networking:** FishNet or Netcode for GameObjects, **host-authoritative**.
- Host simulates all rigidbodies. Do not attempt distributed physics ownership as a v1 architecture.
- Client-side prediction on **your own character controller only**. Everything else is interpolated from host snapshots at roughly 20 Hz.
- Grab: client requests, host validates and parents. Accept ~100ms of grab latency. It is a slow game and this is fine.
- Do not build rollback netcode. You do not need it and it will eat your entire schedule.

**Transport:** Steam Datagram Relay via Steamworks.NET. Solves NAT traversal, gives you Steam lobbies and friend invites for free, and friend invites are your distribution model.

**Voice:** Steam Voice API. Free, no third-party account, no per-minute cost. Apply low-pass filter and distance attenuation client-side.

**Persistence:** none in v1. No accounts, no progression, no unlocks. Session-scoped state only.

---

## 8. Two-week grey-box prototype

Ship nothing but this. No art, no menus, no polish.

**Must exist:**
- One room, 40m x 20m, grey boxes and one crane.
- Six identical capsule players with number labels floating above them (yes, this violates the diegetic rule; it is a prototype).
- Proximity voice with distance falloff and wall occlusion.
- One heavy object requiring two players to lift.
- Three radiation point sources and one movable shield.
- Wrist dosimeter with a readout and a geiger audio loop.
- The torque-sequence bolt puzzle, because it is the purest test of private information.
- Random traitor assignment with the zero-traitor case included.
- The lockout lever.
- A post-round report screen, plain text.

**Must not exist:** any art, any menu, any settings, any tutorial, any progression, any second level.

**The kill test.** Four friends, one voice call, one twenty-minute session. Then answer three questions honestly:

1. Did anyone accuse anyone without being prompted to?
2. Was there at least one moment where the accused player's defence was genuinely convincing to you?
3. On the zero-traitor round, did the crew still turn on each other?

If the answer to any of these is no, the deniability engine is not firing. Do not proceed to art. Adjust the procedures until the answer is yes to all three, or kill the project. This is testable in one evening, which is the entire reason to build this genre.

---

## 9. Anti-slop constraints

Treat this section as binding.

- No emoji anywhere, in game, in UI, in store page, in code comments.
- No floating world-space UI in the shipped build.
- No tutorial popups. Teach through the work order and through failure.
- No AI features, no procedural generation of dialogue, no generated art in shipped assets.
- No purple-to-blue gradients. No glassmorphism. No rounded cards. No drop shadows on flat panels.
- No character customisation, no cosmetics, no battle pass, no currency, no daily login. The suits are identical and that is the point.
- No voice lines, no narrator, no radio operator character explaining things to the player. The only voices in the game are the players.
- No jump scares. The horror is arithmetic.
- Do not add a monster. The moment a monster appears, the crew stops suspecting each other and the game dies.

---

## 10. Milestones

**Weeks 1–2:** Grey-box prototype. Run the kill test.
**Weeks 3–8:** Second and third procedure types. Tune the deniability of each sabotage vector by logging what innocents actually do wrong and making sure the traitor's options overlap.
**Weeks 9–16:** First art pass on one complete facility. Diegetic UI implementation. Audio system as designed, not as an afterthought.
**Weeks 17–20:** Steam page live, capsule art, trailer cut entirely from real recorded sessions with player voice audio intact. Begin posting clips. Wishlists compound and they do not appear in week one.
**Weeks 21–28:** Next Fest demo, two facilities, playtest at scale, netcode hardening.
**Launch:** $12–15, four to six facilities, no roadmap promises you cannot keep.

---

## 11. The trailer, which you should design now

Thirty seconds. No music, no cuts to a logo, no voiceover.

One continuous recorded session. Three people trying to align a crane by shouting numbers at each other. Someone drops the assembly. Alarms. Overlapping panic. One person says "that was not me." Cut to black on the report card revealing there was no traitor this round.

If you cannot cut that trailer from real footage of your own prototype, the game is not working yet.