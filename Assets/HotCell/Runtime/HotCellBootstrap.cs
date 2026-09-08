using System;
using System.Collections.Generic;
using System.Text;
using HotCell.Core;
using UnityEngine;

namespace HotCell
{
    // Runtime geometry is identical on every peer; only the host simulates the work.
    public sealed class HotCellBootstrap : MonoBehaviour
    {
        private const float Ceiling = 100, ShiftSeconds = 1200, TickSeconds = .05f;
        private const int Ready = 0, Working = 1, Finished = 2;
        private sealed class Suit
        {
            public ulong Client;
            public int Slot, LastSequence = -1;
            public bool Connected = true, Traitor, Ejected, Collapsed;
            public float Dose, Rate, LastInput, LastVoice = -10, InspectTime;
            public int InspectTarget = -1;
            public InputFrame Input = new InputFrame();
            public string Action = "";
        }

        private Facility facility;
        private SessionLink link;
        private VoiceAudio audioSystem;
        private VisorEffect visor;
        private Camera view;
        private readonly CrewRig[] rigs = new CrewRig[6];
        private readonly Suit[] crew = new Suit[6];
        private readonly Dictionary<ulong, Suit> byClient = new Dictionary<ulong, Suit>();
        private readonly TorqueProcedure torque = new TorqueProcedure();
        private LockoutHold lockout = new LockoutHold();
        private InputFrame input = new InputFrame();
        private ShiftFrame local;
        private TextMesh wristText, paperText, lockoutPlate;
        private GameObject wrist, paper;
        private Transform fuelAssembly;
        private Vector3 caskStart, shieldStart, doorStart;
        private Quaternion caskRotation, shieldRotation;
        private int phase, tick, localSlot = -1, sequence, wrenchSlot = -1, wrenchBolt = -1;
        private float remaining = ShiftSeconds, networkTime, inputTime, craneTravel, hoistY = 5.5f;
        private bool doorOpen, ventilation = true, loaded, breached, delivered;
        private string report = "SHIFT REPORT\nAWAITING DISPATCH";
        private string startNotice = "Four to six suits required.";

        private void Start()
        {
            Application.runInBackground = true;
            Time.fixedDeltaTime = TickSeconds;
            facility = Facility.Build();
            caskStart = facility.Cask.position; caskRotation = facility.Cask.rotation;
            shieldStart = facility.Shield.position; shieldRotation = facility.Shield.rotation;
            doorStart = facility.Door.position;
            fuelAssembly = facility.CraneHook.Find("Fuel assembly");
            for (int i = 0; i < 6; i++) rigs[i] = CrewRig.Create(i);
            GameObject cameraObject = new GameObject("Suit camera");
            cameraObject.tag = "MainCamera";
            view = cameraObject.AddComponent<Camera>();
            view.nearClipPlane = .04f; view.farClipPlane = 70; view.fieldOfView = 75;
            view.backgroundColor = new Color(.06f, .06f, .05f);
            view.clearFlags = CameraClearFlags.SolidColor;
            view.transform.position = facility.SpawnPoint(0) + Vector3.up * 1.55f;
            cameraObject.AddComponent<AudioListener>();
            visor = cameraObject.AddComponent<VisorEffect>();
            BuildHandObjects();
            lockoutPlate = NewText("Selected suit", facility.Targets["lockout"],
                new Vector3(0, .65f, -.12f), .07f, Color.white);
            lockoutPlate.text = "SELECTED SUIT 01";
            audioSystem = gameObject.AddComponent<VoiceAudio>();
            audioSystem.FindSpeaker = slot => slot >= 0 && slot < 6 && rigs[slot].gameObject.activeSelf ? rigs[slot].transform : null;
            audioSystem.IsOccluded = (a, b) => facility.IsShielded(a, b);
            link = gameObject.AddComponent<SessionLink>();
            link.CanJoin = () => phase != Working && ConnectedCount() < 6;
            link.Joined = Join;
            link.Left = Leave;
            link.InputReceived = AcceptInput;
            link.SnapshotReceived = ApplySnapshot;
            link.VoiceReceived = RelayVoice;
            link.VoicePlayback = (slot, pcm) => audioSystem.Receive(slot, pcm);
            audioSystem.SendVoice = pcm => link.SendVoice(pcm);
            string[] args = Environment.GetCommandLineArgs();
            string address = Argument(args, "-hotcell-join", "");
            string portText = Argument(args, "-hotcell-port", "7777");
            if (!ushort.TryParse(portText, out ushort port) || port == 0) port = 7777;
            bool host = string.IsNullOrEmpty(address);
            facility.Cask.isKinematic = !host;
            facility.Shield.isKinematic = !host;
            if (!link.Open(host, host ? "127.0.0.1" : address, port))
                facility.Report.text = "NETWORK START FAILED\nCheck the player log and port " + port;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static string Argument(string[] args, string key, string fallback)
        {
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1];
            return fallback;
        }

        private void Join(ulong id)
        {
            if (!link.IsHost || byClient.ContainsKey(id)) return;
            for (int i = 0; i < 6; i++)
            {
                if (crew[i] != null && crew[i].Connected) continue;
                Suit suit = new Suit { Client = id, Slot = i, LastInput = Time.unscaledTime };
                crew[i] = suit; byClient[id] = suit;
                rigs[i].gameObject.SetActive(true);
                rigs[i].Teleport(facility.SpawnPoint(i));
                break;
            }
        }

        private void Leave(ulong id)
        {
            if (!link.IsHost) return;
            if (!byClient.TryGetValue(id, out Suit suit)) return;
            suit.Connected = false; suit.Action = "";
            byClient.Remove(id);
            rigs[suit.Slot].gameObject.SetActive(false);
            if (lockout.IsActive && (lockout.Actor == suit.Slot + 1 || lockout.Target == suit.Slot + 1)) lockout.Cancel();
        }

        private int ConnectedCount()
        {
            int count = 0;
            foreach (Suit suit in crew) if (suit != null && suit.Connected) count++;
            return count;
        }

        private void AcceptInput(ulong id, InputFrame frame)
        {
            if (!byClient.TryGetValue(id, out Suit suit) || !suit.Connected || frame == null) return;
            if (frame.sequence <= suit.LastSequence || !Finite(frame.horizontal) || !Finite(frame.vertical) ||
                !Finite(frame.yaw) || !Finite(frame.pitch)) return;
            suit.LastSequence = frame.sequence;
            // The host's clock, not the amount of input packets, determines simulation time.
            frame.horizontal = Mathf.Clamp(frame.horizontal, -1, 1);
            frame.vertical = Mathf.Clamp(frame.vertical, -1, 1);
            frame.yaw = Mathf.Repeat(frame.yaw, 360);
            frame.pitch = Mathf.Clamp(frame.pitch, -80, 80);
            frame.selectedSuit = Mathf.Clamp(frame.selectedSuit, 1, 6);
            suit.Input = frame;
            suit.LastInput = Time.unscaledTime;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        private void Update()
        {
            if (link == null) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            if (Input.GetMouseButtonDown(0) && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            }
            if (localSlot >= 0 && link.Net.IsConnectedClient)
            {
                ReadLocalInput();
                inputTime += Time.unscaledDeltaTime;
                if (inputTime >= TickSeconds)
                {
                    inputTime = 0;
                    // A fresh instance prevents host state from sharing mutable local input.
                    InputFrame outgoing = JsonUtility.FromJson<InputFrame>(JsonUtility.ToJson(input));
                    outgoing.sequence = ++sequence;
                    link.SendInput(outgoing);
                }
                if (!link.IsHost && local != null)
                {
                    SuitFrame own = OwnFrame();
                    if (own != null && !own.collapsed && !own.ejected)
                    {
                        rigs[localSlot].Move(input, Time.deltaTime, Speed(local.localDose));
                        Vector3 correction = rigs[localSlot].TargetPosition - rigs[localSlot].transform.position;
                        if (correction.sqrMagnitude > 4) rigs[localSlot].Teleport(rigs[localSlot].TargetPosition);
                        else rigs[localSlot].Controller.Move(correction * Mathf.Clamp01(Time.deltaTime * 2));
                    }
                }
                UpdateHands();
            }
            if (!link.IsHost && local != null)
            {
                for (int i = 0; i < rigs.Length; i++)
                    if (i != localSlot && rigs[i].gameObject.activeSelf) rigs[i].Interpolate(Time.deltaTime);
                facility.Cask.position = Vector3.Lerp(facility.Cask.position, local.caskPosition, 1 - Mathf.Exp(-16 * Time.deltaTime));
                facility.Cask.rotation = Quaternion.Slerp(facility.Cask.rotation, local.caskRotation, 1 - Mathf.Exp(-16 * Time.deltaTime));
                facility.Shield.position = Vector3.Lerp(facility.Shield.position, local.shieldPosition, 1 - Mathf.Exp(-16 * Time.deltaTime));
                facility.Shield.rotation = Quaternion.Slerp(facility.Shield.rotation, local.shieldRotation, 1 - Mathf.Exp(-16 * Time.deltaTime));
                facility.CraneHook.position = Vector3.Lerp(facility.CraneHook.position, local.hookPosition, 1 - Mathf.Exp(-16 * Time.deltaTime));
            }
            if (local != null)
            {
                if (!link.IsHost) facility.Door.position = Vector3.Lerp(facility.Door.position, doorStart + Vector3.up * (local.doorOpen ? 4.2f : 0), Time.deltaTime * 3);
                if (local.ventilation) facility.VentFan.Rotate(Vector3.forward, 220 * Time.deltaTime);
                if (fuelAssembly != null) fuelAssembly.gameObject.SetActive(!local.loaded);
                int seconds = Mathf.CeilToInt(local.remaining);
                facility.Clock.text = "SHIFT CLOCK\n" + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
                facility.Report.text = local.phase == Finished ? local.report : "SHIFT REPORT\n" + (local.phase == Ready ? startNotice : "AWAITING DISPATCH");
                facility.CraneGauge.text = "HOIST LOAD\n" + (local.loaded ? "0000" : "0450") + " KG";
                lockoutPlate.text = "SELECTED SUIT " + (local.lockoutTarget > 0 ? local.lockoutTarget : input.selectedSuit).ToString("00");
                audioSystem.DoseRate = local.localRate;
                audioSystem.DoseFraction = local.localDose / Ceiling;
                audioSystem.Exertion = new Vector2(input.horizontal, input.vertical).magnitude;
                audioSystem.VentilationRunning = local.ventilation;
                audioSystem.Alarm = local.alarm;
                visor.DoseFraction = local.localDose / Ceiling;
            }
            if (link.Status.StartsWith("DISCONNECTED")) facility.Report.text = link.Status;
        }

        private void ReadLocalInput()
        {
            bool focused = Cursor.lockState == CursorLockMode.Locked && Application.isFocused;
            if (localSlot >= 0 && local != null && OwnFrame() != null && OwnFrame().ejected)
                rigs[localSlot].transform.rotation = Quaternion.Euler(0, input.yaw, 0);
            input.horizontal = focused ? (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0) : 0;
            input.vertical = focused ? (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0) : 0;
            if (focused)
            {
                input.yaw += Input.GetAxisRaw("Mouse X") * 2;
                input.pitch = Mathf.Clamp(input.pitch - Input.GetAxisRaw("Mouse Y") * 2, -80, 80);
            }
            input.use = focused && Input.GetKey(KeyCode.E);
            input.reverse = focused && Input.GetKey(KeyCode.Q);
            if (focused && Input.GetKeyDown(KeyCode.F)) input.lamp = !input.lamp;
            if (focused && Input.GetKeyDown(KeyCode.R)) input.clipboard = !input.clipboard;
            input.wrist = focused && Input.GetKey(KeyCode.T);
            for (int i = 0; i < 6; i++)
                if (focused && Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) input.selectedSuit = i + 1;
            if (localSlot >= 0)
            {
                rigs[localSlot].Head.localRotation = Quaternion.Euler(input.pitch, 0, 0);
                rigs[localSlot].Lamp.enabled = input.lamp;
            }
        }

        private SuitFrame OwnFrame()
        {
            if (local == null || local.suits == null) return null;
            foreach (SuitFrame suit in local.suits) if (suit.slot == localSlot) return suit;
            return null;
        }

        private void FixedUpdate()
        {
            if (link == null || !link.IsHost) return;
            tick++;
            foreach (Suit suit in crew)
            {
                if (suit == null || !suit.Connected) continue;
                if (Time.unscaledTime - suit.LastInput > .25f)
                {
                    suit.Input.use = false; suit.Input.horizontal = 0; suit.Input.vertical = 0;
                }
                if (!suit.Collapsed && !suit.Ejected)
                    rigs[suit.Slot].Move(suit.Input, TickSeconds, Speed(suit.Dose));
                suit.Action = GetAction(suit);
                if (phase == Working && !suit.Ejected)
                {
                    suit.Rate = Exposure(rigs[suit.Slot].transform.position + Vector3.up);
                    suit.Dose = RadiationRules.Accumulate(suit.Dose, suit.Rate, TickSeconds);
                    suit.Collapsed = RadiationRules.Stage(suit.Dose, Ceiling) == DoseStage.Collapsed;
                    if (suit.Collapsed) suit.Action = "";
                }
                else suit.Rate = 0;
            }
            if (phase != Working)
            {
                Suit host = FindAction("start");
                if (host != null && host.Client == link.Net.LocalClientId)
                {
                    if (ConnectedCount() >= 4) BeginShift();
                    else startNotice = "FOUR TO SIX SUITS REQUIRED\nCONNECTED: " + ConnectedCount();
                }
            }
            if (phase == Working)
            {
                remaining = Mathf.Max(0, remaining - TickSeconds);
                SimulateCrane();
                SimulateBolts();
                Carry(facility.Cask, "cask", 2, 1.25f);
                Carry(facility.Shield, "shield", 1, 1.1f);
                SimulateServices();
                SimulateInspection();
                SimulateLockout();
                if (loaded && facility.Cask.linearVelocity.magnitude > 8) breached = true;
                if (FindAction("extract") != null && loaded && Vector3.Distance(facility.Cask.position, new Vector3(-15, .85f, 4)) < 3)
                {
                    delivered = true; breached |= !torque.IsSealed; FinishShift();
                }
                else if (breached || remaining <= 0 || AvailableSurvivors() < 3) FinishShift();
            }
            networkTime += TickSeconds;
            if (networkTime >= TickSeconds)
            {
                networkTime = 0;
                foreach (Suit suit in crew) if (suit != null && suit.Connected) link.SendState(suit.Client, Snapshot(suit));
            }
        }

        private static float Speed(float dose) { return dose >= 75 ? 1.25f : dose >= 50 ? 1.8f : 2.6f; }

        private string GetAction(Suit suit)
        {
            if (!suit.Input.use || suit.Input.wrist || suit.Input.clipboard || suit.Ejected || suit.Collapsed) return "";
            Vector3 origin = rigs[suit.Slot].transform.position + Vector3.up * 1.55f;
            Vector3 direction = Quaternion.Euler(suit.Input.pitch, suit.Input.yaw, 0) * Vector3.forward;
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return "";
            WorkTarget target = hit.collider.GetComponentInParent<WorkTarget>();
            return target == null ? "" : target.Id;
        }

        private Suit FindAction(string action, int except = -1)
        {
            foreach (Suit suit in crew)
                if (suit != null && suit.Connected && suit.Slot != except && suit.Action == action && !suit.Ejected && !suit.Collapsed) return suit;
            return null;
        }

        private void BeginShift()
        {
            var active = new List<Suit>();
            for (int i = 0; i < crew.Length; i++)
            {
                if (crew[i] != null && crew[i].Connected) active.Add(crew[i]);
                else crew[i] = null;
            }
            bool[] roles = ShiftRules.AssignTraitors(active.Count, new System.Random());
            for (int i = 0; i < active.Count; i++)
            {
                Suit suit = active[i];
                suit.Traitor = roles[i]; suit.Dose = suit.Rate = 0; suit.Collapsed = suit.Ejected = false;
                suit.Action = ""; suit.InspectTarget = -1; suit.InspectTime = 0;
                rigs[suit.Slot].Teleport(facility.SpawnPoint(suit.Slot));
            }
            torque.Reset(); lockout = new LockoutHold();
            ResetBody(facility.Cask, caskStart, caskRotation);
            ResetBody(facility.Shield, shieldStart, shieldRotation);
            craneTravel = 0; hoistY = 5.5f;
            loaded = breached = delivered = doorOpen = false;
            ventilation = true; wrenchSlot = wrenchBolt = -1;
            remaining = ShiftSeconds; phase = Working;
            report = "SHIFT REPORT\nAWAITING DISPATCH";
        }

        private static void ResetBody(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            body.position = position; body.rotation = rotation;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        }

        private void SimulateCrane()
        {
            if (loaded) return;
            Suit hoist = FindAction("hoist") ?? FindAction("crane_down") ?? FindAction("crane_up");
            Suit traverse = FindAction("traverse", hoist == null ? -1 : hoist.Slot);
            if (hoist != null && traverse != null)
            {
                bool lower = hoist.Action == "crane_down" || (hoist.Action == "hoist" && hoist.Input.reverse);
                hoistY = Mathf.Clamp(hoistY + (lower ? -1 : 1) * .25f * TickSeconds, 3.25f, 5.5f);
                craneTravel = Mathf.Clamp01(craneTravel + (traverse.Input.reverse ? -1 : 1) * .045f * TickSeconds);
                if (hoistY <= 3.26f)
                {
                    if (craneTravel >= .95f && Vector3.Distance(facility.Cask.position, caskStart) < .5f) loaded = true;
                    else breached = true;
                }
            }
            facility.CraneHook.position = new Vector3(Mathf.Lerp(10, 8, craneTravel), hoistY, Mathf.Lerp(3, 0, craneTravel));
        }

        private void SimulateBolts()
        {
            wrenchSlot = wrenchBolt = -1;
            if (!loaded) return;
            Suit lid = FindAction("lid");
            foreach (Suit suit in crew)
            {
                if (suit == null || !suit.Connected || suit.Ejected || suit.Collapsed || !suit.Action.StartsWith("bolt")) continue;
                if (!int.TryParse(suit.Action.Substring(4), out int bolt) || bolt < 0 || bolt >= 8) continue;
                // Only one wrench exists; its gauge is sent solely to the current holder.
                wrenchSlot = suit.Slot; wrenchBolt = bolt;
                if (lid != null && lid.Slot != suit.Slot)
                {
                    float amount = suit.Input.reverse ? -16 : 8;
                    if (suit.Dose >= 50) amount *= 1 + .35f * Mathf.Sin(tick * 1.7f + suit.Slot);
                    torque.Apply(bolt, amount * TickSeconds);
                    facility.Bolts[bolt].Rotate(Vector3.up, amount * TickSeconds * 5, Space.Self);
                }
                break;
            }
            bool allLoose = true;
            for (int i = 0; i < 8; i++) if (torque.TorqueAt(i) > 0) allLoose = false;
            if (allLoose) torque.Reset();
        }

        private void Carry(Rigidbody body, string target, int required, float height)
        {
            Vector3 destination = Vector3.zero;
            int hands = 0;
            foreach (Suit suit in crew)
            {
                if (suit == null || !suit.Connected || suit.Ejected || suit.Collapsed || suit.Action != target) continue;
                if (suit.Dose >= 75 && Mathf.Sin(tick * .07f + suit.Slot) > .82f) continue;
                Vector3 p = rigs[suit.Slot].transform.position;
                destination += p + rigs[suit.Slot].transform.forward * 1.25f;
                hands++;
            }
            body.linearDamping = hands >= required ? 5 : .8f;
            if (hands < required) return;
            destination /= hands; destination.y = height;
            Vector3 acceleration = (destination - body.position) * 14 - body.linearVelocity * 3 + Vector3.up * 9.81f;
            body.AddForce(Vector3.ClampMagnitude(acceleration, 22), ForceMode.Acceleration);
        }

        private void SimulateServices()
        {
            Suit door = FindAction("door");
            if (door != null) doorOpen = !door.Input.reverse;
            Suit vent = FindAction("vent");
            if (vent != null) ventilation = !vent.Input.reverse;
            // Move on the host's physics tick so raycasts use authoritative shielding.
            facility.Door.position = Vector3.MoveTowards(facility.Door.position, doorStart + Vector3.up * (doorOpen ? 4.2f : 0), TickSeconds * 4);
            Physics.SyncTransforms();
        }

        private void SimulateInspection()
        {
            foreach (Suit suit in crew)
            {
                if (suit == null || !suit.Connected) continue;
                int target = -1;
                if (suit.Action.StartsWith("suit") && !int.TryParse(suit.Action.Substring(4), out target)) target = -1;
                bool valid = target >= 0 && target < 6 && target != suit.Slot && crew[target] != null && crew[target].Connected;
                if (valid)
                {
                    Suit other = crew[target];
                    valid = new Vector2(other.Input.horizontal, other.Input.vertical).sqrMagnitude < .01f &&
                        new Vector2(suit.Input.horizontal, suit.Input.vertical).sqrMagnitude < .01f &&
                        Vector3.Distance(rigs[target].transform.position, rigs[suit.Slot].transform.position) <= 1.8f;
                }
                if (!valid) { suit.InspectTarget = -1; suit.InspectTime = 0; continue; }
                if (suit.InspectTarget != target) { suit.InspectTarget = target; suit.InspectTime = 0; }
                suit.InspectTime += TickSeconds;
            }
        }

        private void SimulateLockout()
        {
            Suit actor = lockout.IsActive && crew[lockout.Actor - 1] != null ? crew[lockout.Actor - 1] : FindAction("lockout");
            if (actor == null) { lockout.Cancel(); return; }
            int selected = actor.Input.selectedSuit;
            Suit target = crew[selected - 1];
            bool valid = actor.Action == "lockout" && target != null && target.Connected && !target.Ejected && !target.Collapsed && target != actor;
            if (!valid) { lockout.Cancel(); return; }
            if (!lockout.IsActive) lockout.Begin(actor.Slot + 1, selected);
            if (lockout.Tick(actor.Slot + 1, selected, TickSeconds, valid))
            {
                target.Ejected = true; target.Action = "";
                rigs[target.Slot].Teleport(facility.ObservationPoint(target.Slot));
            }
        }

        private float Exposure(Vector3 sample)
        {
            float total = 0;
            for (int i = 0; i < facility.Sources.Length; i++)
            {
                Vector3 source = i == 0 ? (loaded ? facility.Cask.position : facility.CraneHook.position + Vector3.down) : facility.Sources[i];
                float strength = i == 0 ? .7f : .18f;
                float transmission = facility.Transmission(source, sample);
                if (i == 0 && loaded && torque.IsSealed) transmission *= .025f;
                total += RadiationRules.Rate(strength, (source - sample).sqrMagnitude, transmission);
            }
            if (!ventilation) total += .035f;
            return total;
        }

        private int AvailableSurvivors()
        {
            int count = 0;
            foreach (Suit suit in crew) if (suit != null && suit.Connected && !suit.Collapsed && suit.Dose < Ceiling) count++;
            return count;
        }

        private void FinishShift()
        {
            if (phase != Working) return;
            phase = Finished; lockout.Cancel();
            var outcomes = new List<CrewOutcome>();
            StringBuilder lines = new StringBuilder();
            int traitors = 0;
            foreach (Suit suit in crew)
            {
                if (suit == null) continue;
                bool evacuated = suit.Connected && (suit.Ejected || rigs[suit.Slot].transform.position.x < -10);
                outcomes.Add(new CrewOutcome(suit.Slot + 1, suit.Traitor, suit.Dose, evacuated, suit.Collapsed || !suit.Connected));
                if (suit.Traitor) traitors++;
                lines.AppendFormat("SUIT {0:00}  DOSE {1:000.0}  {2}  {3}\n", suit.Slot + 1, suit.Dose,
                    suit.Traitor ? "TRAITOR" : "CREW", !suit.Connected ? "LOST LINK" : suit.Collapsed ? "COLLAPSED" : evacuated ? "OUT" : "LEFT INSIDE");
            }
            RoundOutcome result = ShiftRules.Resolve(!breached && (!delivered || (loaded && torque.IsSealed)), delivered, remaining <= 0, outcomes, Ceiling);
            report = "POST-SHIFT REPORT\n\n" + result.Summary + "\n\n" + lines + "\nTRAITORS THIS SHIFT: " + traitors +
                "\nCONTAINMENT: " + (breached ? "BREACHED" : loaded && torque.IsSealed ? "SEALED" : "OPEN / INCOMPLETE") +
                "\nTRANSPORT: " + (delivered ? "DELIVERED" : "NOT DELIVERED") + "\n\nHOST: RETURN TO SHIFT START FOR NEXT ORDER.";
            // The report keeps the final outcome. Return everyone to the ready room for the next shift.
            foreach (Suit suit in crew)
            {
                if (suit == null || !suit.Connected) continue;
                suit.Ejected = suit.Collapsed = false; suit.Action = ""; suit.Rate = 0;
                rigs[suit.Slot].Teleport(facility.SpawnPoint(suit.Slot));
            }
        }

        private ShiftFrame Snapshot(Suit recipient)
        {
            var suits = new List<SuitFrame>();
            foreach (Suit suit in crew)
                if (suit != null) suits.Add(new SuitFrame
                {
                    slot = suit.Slot, connected = suit.Connected,
                    position = rigs[suit.Slot].transform.position, yaw = suit.Input.yaw,
                    collapsed = suit.Collapsed, ejected = suit.Ejected, lamp = suit.Input.lamp
                });
            bool reading = recipient.InspectTime >= 2 && recipient.InspectTarget >= 0;
            return new ShiftFrame
            {
                tick = tick, localSlot = recipient.Slot, phase = phase, suits = suits.ToArray(),
                caskPosition = facility.Cask.position, caskRotation = facility.Cask.rotation,
                shieldPosition = facility.Shield.position, shieldRotation = facility.Shield.rotation,
                hookPosition = facility.CraneHook.position,
                doorOpen = doorOpen, ventilation = ventilation, loaded = loaded,
                alarm = lockout.IsActive || breached, remaining = remaining,
                localDose = recipient.Dose, localRate = recipient.Rate,
                localTraitor = phase != Ready && recipient.Traitor,
                gaugeBolt = recipient.Slot == wrenchSlot ? wrenchBolt : -1,
                gaugeTorque = recipient.Slot == wrenchSlot && wrenchBolt >= 0 ? torque.TorqueAt(wrenchBolt) : 0,
                inspectedSuit = reading ? recipient.InspectTarget + 1 : 0,
                inspectedDose = reading ? crew[recipient.InspectTarget].Dose : 0,
                lockoutTarget = lockout.IsActive ? lockout.Target : 0,
                report = phase == Finished ? report : ""
            };
        }

        private void ApplySnapshot(ShiftFrame frame)
        {
            if (frame == null || frame.localSlot < 0 || frame.localSlot >= 6 || frame.suits == null || frame.suits.Length > 6) return;
            if (local != null && frame.tick <= local.tick) return;
            bool phaseChanged = local == null || local.phase != frame.phase;
            local = frame;
            for (int i = 0; i < 6; i++)
            {
                SuitFrame state = Array.Find(frame.suits, x => x.slot == i);
                if (state == null) { rigs[i].gameObject.SetActive(false); continue; }
                bool newRig = !rigs[i].gameObject.activeSelf;
                rigs[i].gameObject.SetActive(state.connected);
                if (!state.connected) continue;
                rigs[i].TargetPosition = state.position; rigs[i].TargetYaw = state.yaw;
                rigs[i].Lamp.enabled = state.lamp;
                if (!link.IsHost && (newRig || phaseChanged || state.ejected || state.collapsed)) rigs[i].Teleport(state.position);
                // Collapsed pose is public, but its exact dose is not.
                rigs[i].Head.localPosition = Vector3.up * (state.collapsed ? .55f : 1.55f);
            }
            if (localSlot != frame.localSlot)
            {
                localSlot = frame.localSlot;
                for (int i = 0; i < 6; i++) rigs[i].MakeLocal(i == localSlot);
                view.transform.SetParent(rigs[localSlot].Head, false);
                view.transform.localPosition = Vector3.zero;
                view.transform.localRotation = Quaternion.identity;
                audioSystem.LocalSlot = localSlot;
            }
        }

        private void RelayVoice(ulong sender, byte[] pcm)
        {
            if (!link.IsHost || !byClient.TryGetValue(sender, out Suit speaker) || !speaker.Connected) return;
            if (Time.unscaledTime - speaker.LastVoice < .035f) return;
            speaker.LastVoice = Time.unscaledTime;
            foreach (Suit listener in crew)
            {
                if (listener == null || !listener.Connected || listener.Client == sender) continue;
                if (Vector3.Distance(rigs[speaker.Slot].transform.position, rigs[listener.Slot].transform.position) <= 18)
                    link.RelayVoice(listener.Client, speaker.Slot, pcm);
            }
        }

        private void BuildHandObjects()
        {
            wrist = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wrist.name = "Wrist dosimeter";
            Destroy(wrist.GetComponent<Collider>());
            wrist.transform.SetParent(view.transform, false);
            wrist.transform.localPosition = new Vector3(-.17f, -.13f, .4f);
            wrist.transform.localScale = new Vector3(.19f, .09f, .07f);
            wrist.GetComponent<Renderer>().material.color = new Color(.18f, .18f, .16f);
            wristText = NewText("Dosimeter LCD", view.transform, new Vector3(-.25f, -.09f, .355f), .012f, new Color(.8f, .78f, .5f));
            paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "Carried work order";
            Destroy(paper.GetComponent<Collider>());
            paper.transform.SetParent(view.transform, false);
            paper.transform.localPosition = new Vector3(.05f, -.06f, .55f);
            paper.transform.localScale = new Vector3(.66f, .45f, .013f);
            paper.GetComponent<Renderer>().material.color = new Color(.64f, .62f, .53f);
            paperText = NewText("Printed order", view.transform, new Vector3(-.25f, .13f, .53f), .011f, new Color(.09f, .09f, .08f));
            wrist.SetActive(false); paper.SetActive(false);
            wristText.gameObject.SetActive(false); paperText.gameObject.SetActive(false);
        }

        private static TextMesh NewText(string name, Transform parent, Vector3 localPosition, float size, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false); obj.transform.localPosition = localPosition;
            TextMesh text = obj.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<Renderer>().sharedMaterial = text.font.material;
            WorldText.Configure(text, size, color); text.anchor = TextAnchor.UpperLeft;
            return text;
        }

        private void UpdateHands()
        {
            if (local == null) return;
            bool showWrist = input.wrist || local.gaugeBolt >= 0 || local.inspectedSuit > 0;
            wrist.SetActive(showWrist); wristText.gameObject.SetActive(showWrist);
            paper.SetActive(input.clipboard); paperText.gameObject.SetActive(input.clipboard);
            wristText.text = local.gaugeBolt >= 0 ? "B" + (local.gaugeBolt + 1) + " " + local.gaugeTorque.ToString("000.0") + " NM" :
                local.inspectedSuit > 0 ? "SUIT " + local.inspectedSuit.ToString("00") + "\n" + local.inspectedDose.ToString("000.0") : local.localDose.ToString("000.0");
            paperText.text = "WORK ORDER 01 / SUIT " + (localSlot + 1).ToString("00") + "\n20 MIN / DOSE CEILING 100\n\n" +
                "HOIST + TRAVERSE: TWO OPERATORS.\nSPOTTER CALLS THE ALIGNMENT.\nLOWER INTO CASK AT END OF TRAVERSE.\n\n" +
                "PARTNER HOLDS LID. EACH BOLT: 80 +/-5 NM.\nORDER: 1 5 3 7 2 6 4 8.\nQ+E BACKS OFF. ALL ZERO RESETS SEQUENCE.\n\n" +
                "TWO HANDLERS TAKE CASK TO TRANSPORT.\nASSEMBLE IN TRANSPORT OR READY ROOM.\nDISPATCH ENDS THE SHIFT.\n\n" +
                (local.phase == Ready ? "AWAITING SHIFT ASSIGNMENT." : local.localTraitor ? "PRIVATE CONTRACT: PREVENT DISPATCH.\nYOU MUST LEAVE ALIVE BELOW THE CEILING." : "PRIVATE CONTRACT: COMPLETE THE WORK.");
            float tremor = local.localDose >= 50 ? .003f * Mathf.Sin(Time.time * 23) : 0;
            wrist.transform.localPosition = new Vector3(-.17f + tremor, -.13f, .4f);
            view.transform.localRotation = Quaternion.Euler(0, 0, local.localDose >= 50 ? Mathf.Sin(Time.time * 7) * .2f : 0);
        }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (view != null) Destroy(view.gameObject);
            if (facility != null) Destroy(facility.gameObject);
            foreach (CrewRig rig in rigs) if (rig != null) Destroy(rig.gameObject);
        }
    }
}
