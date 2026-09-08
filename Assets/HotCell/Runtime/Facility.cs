using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotCell
{
    public sealed class WorkTarget : MonoBehaviour
    {
        public string Id;
    }

    public sealed class ShieldMaterial : MonoBehaviour
    {
        [Range(0f, 1f)] public float Factor = 0.1f;
    }

    /// <summary>A deliberately primitive, metre-scale room assembled at runtime.</summary>
    public sealed class Facility : MonoBehaviour
    {
        public Rigidbody Cask, Shield;
        public Transform CraneHook, Door, VentFan;
        public Transform[] Bolts;
        public TextMesh WorkOrder, Clock, CraneGauge, Report;
        public Vector3[] Sources;
        public readonly Dictionary<string, Transform> Targets = new Dictionary<string, Transform>();

        readonly RaycastHit[] shieldingHits = new RaycastHit[128];
        readonly HashSet<int> seenShields = new HashSet<int>();
        readonly List<Material> ownedMaterials = new List<Material>();
        Material concrete, steel, pale, dark, yellow, red, lead, water;
        Font font;
        Transform craneBridge, craneTrolley, hoistCable;

        public static Facility Build()
        {
            var facility = new GameObject("HOT CELL / Greybox facility").AddComponent<Facility>();
            facility.Construct();
            return facility;
        }

        public Vector3 SpawnPoint(int slot)
        {
            slot = Mathf.Clamp(slot, 0, 5);
            return new Vector3(-17.5f + (slot % 3) * 1.6f, 0.05f, -6.8f + (slot / 3) * 1.7f);
        }

        public Vector3 ObservationPoint(int slot)
        {
            slot = Mathf.Clamp(slot, 0, 5);
            return new Vector3(-18.4f + (slot % 3) * 1.3f, 0.05f, -8.0f + (slot / 3) * 1.15f);
        }

        public bool IsShielded(Vector3 source, Vector3 sample) => Transmission(source, sample) < 0.5f;

        public float Transmission(Vector3 source, Vector3 sample)
        {
            var delta = sample - source;
            var distance = delta.magnitude;
            if (distance < 0.01f) return 1f;
            seenShields.Clear();
            float transmission = 1f;
            int count = Physics.RaycastNonAlloc(source, delta / distance, shieldingHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var material = shieldingHits[i].collider.GetComponentInParent<ShieldMaterial>();
                if (material != null && seenShields.Add(material.GetInstanceID()))
                    transmission *= Mathf.Clamp01(material.Factor);
            }
            return Mathf.Clamp01(transmission);
        }

        void Construct()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            concrete = MakeMaterial("Unpainted concrete", new Color(0.33f, 0.34f, 0.32f));
            steel = MakeMaterial("Oxidised steel", new Color(0.29f, 0.25f, 0.21f), 0.45f);
            pale = MakeMaterial("Dirty white paint", new Color(0.74f, 0.73f, 0.66f));
            dark = MakeMaterial("Blackened steel", new Color(0.09f, 0.10f, 0.10f), 0.35f);
            yellow = MakeMaterial("Hazard yellow", new Color(0.82f, 0.62f, 0.04f));
            red = MakeMaterial("Alarm red", new Color(0.70f, 0.07f, 0.025f));
            lead = MakeMaterial("Lead shielding", new Color(0.25f, 0.28f, 0.28f), 0.15f);
            water = MakeMaterial("Pool surface", new Color(0.14f, 0.18f, 0.18f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.19f, 0.18f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.075f, 0.075f, 0.065f);

            BuildShell();
            BuildReadyRoom();
            BuildCrane();
            BuildCask();
            BuildShield();
            BuildServices();
            Sources = new[] { new Vector3(10f, 1f, 3f), new Vector3(12f, 1f, -4f), new Vector3(5f, 1f, 5f) };
            for (int i = 1; i < Sources.Length; i++)
            {
                var drum = Primitive("Unsealed source container " + i, PrimitiveType.Cylinder,
                    new Vector3(Sources[i].x, 0.53f, Sources[i].z), new Vector3(0.75f, 0.53f, 0.75f), steel);
                Primitive("Source container cap", PrimitiveType.Cylinder,
                    drum.position + Vector3.up * 0.53f, new Vector3(0.8f, 0.035f, 0.8f), yellow);
                Plate("SOURCE " + (i + 1).ToString("00") + "\nKEEP CLEAR", drum.position + new Vector3(0f, 0.1f, -0.4f),
                    Vector3.back, new Vector2(0.7f, 0.4f), 0.07f, yellow, dark.color);
            }
        }

        void BuildShell()
        {
            Box("Slab / clear floor 40 x 20 m", new Vector3(0f, -0.2f, 0f), new Vector3(40f, 0.4f, 20f), concrete);
            Attenuate(Box("West wall", new Vector3(-20.25f, 3.25f, 0f), new Vector3(0.5f, 6.5f, 21f), concrete), 0.04f);
            Attenuate(Box("East wall", new Vector3(20.25f, 3.25f, 0f), new Vector3(0.5f, 6.5f, 21f), concrete), 0.04f);
            Attenuate(Box("South wall", new Vector3(0f, 3.25f, -10.25f), new Vector3(40f, 6.5f, 0.5f), concrete), 0.04f);
            Attenuate(Box("North wall", new Vector3(0f, 3.25f, 10.25f), new Vector3(40f, 6.5f, 0.5f), concrete), 0.04f);
            Box("Ceiling", new Vector3(0f, 6.65f, 0f), new Vector3(40f, 0.3f, 20f), concrete);
            Attenuate(Box("Shield wall south", new Vector3(0f, 3.25f, -6.125f), new Vector3(0.65f, 6.5f, 7.75f), concrete), 0.035f);
            Attenuate(Box("Shield wall north", new Vector3(0f, 3.25f, 6.125f), new Vector3(0.65f, 6.5f, 7.75f), concrete), 0.035f);
            Attenuate(Box("Door lintel", new Vector3(0f, 5.25f, 0f), new Vector3(0.65f, 2.5f, 4.5f), concrete), 0.035f);
            Door = Box("Shield door / vertical slide", new Vector3(0f, 1.95f, 0f), new Vector3(0.4f, 3.9f, 4.2f), lead);
            Attenuate(Door, 0.025f);
            Target("door", Door);
            for (int i = -2; i <= 2; i++)
            {
                var stripe = Box("Door hazard stripe", new Vector3(-0.211f, 0.5f + i * 0.05f + 1f, i * 0.75f),
                    new Vector3(0.015f, 0.17f, 0.6f), yellow);
                stripe.SetParent(Door, true);
            }
            Plate("SHIELD DOOR\nHOLD E / Q", new Vector3(-0.24f, 2.8f, 0f), Vector3.left,
                new Vector2(2f, 0.8f), 0.15f, pale, dark.color, Door);
            Control("Door west control", "door", new Vector3(-0.57f, 1.2f, -2.9f), Vector3.left, "SHIELD DOOR\nE / Q", true);
            Control("Door east control", "door", new Vector3(0.57f, 1.2f, -2.9f), Vector3.right, "SHIELD DOOR\nE / Q", true);

            for (int x = -15; x <= 15; x += 10)
            {
                Box("Ceiling crossbeam", new Vector3(x, 6.28f, 0f), new Vector3(0.3f, 0.4f, 20f), steel);
                for (int z = -6; z <= 6; z += 12)
                {
                    Box("Lamp housing", new Vector3(x, 4.58f, z), new Vector3(1.5f, 0.16f, 0.3f), pale);
                    Lamp(new Vector3(x, 4.3f, z));
                }
            }
            for (int x = -18; x <= 18; x += 4)
                Box("Floor slab joint", new Vector3(x, 0.006f, 0f), new Vector3(0.022f, 0.012f, 20f), dark, false);
            BayLines(new Vector3(-15f, 0.015f, 4f), 5f, 5f);
            Plate("TRANSPORT BAY / CASK 01", new Vector3(-15f, 0.025f, 7f), Vector3.up,
                new Vector2(5f, 0.7f), 0.16f, concrete, pale.color);
            Control("Dispatch handle", "extract", new Vector3(-18.7f, 1.2f, 6f), Vector3.right,
                "DISPATCH\nSEALED CASK ONLY", false);
            Clock = Plate("SHIFT CLOCK\n20:00", new Vector3(-7f, 3.5f, -9.73f), Vector3.forward,
                new Vector2(3.8f, 1.25f), 0.3f, dark, pale.color);
            Plate("HOT CELL\nCONTRACT DECOMMISSIONING", new Vector3(7f, 3.3f, 9.72f), Vector3.back,
                new Vector2(6f, 1.4f), 0.28f, concrete, pale.color);
        }

        void BuildReadyRoom()
        {
            Attenuate(Box("Ready room low wall", new Vector3(-11f, 0.65f, -6.75f), new Vector3(0.28f, 1.3f, 6.5f), concrete), 0.05f);
            Attenuate(Box("Observation lead glass", new Vector3(-11f, 2.2f, -6.75f), new Vector3(0.12f, 1.8f, 6.5f),
                MakeGlassMaterial()), 0.2f);
            Box("Observation frame top", new Vector3(-11f, 3.14f, -6.75f), new Vector3(0.25f, 0.12f, 6.5f), steel);
            for (int i = 0; i < 4; i++)
                Box("Observation mullion", new Vector3(-11f, 2.2f, -9.85f + i * 2.08f), new Vector3(0.2f, 2f, 0.08f), steel);
            Attenuate(Box("Ready room north wall", new Vector3(-16.6f, 1.6f, -3.4f), new Vector3(6.8f, 3.2f, 0.25f), concrete), 0.05f);
            Plate("READY ROOM\nOBSERVATION", new Vector3(-13.8f, 2.2f, -3.23f), Vector3.forward,
                new Vector2(1.5f, 0.7f), 0.12f, pale, dark.color);

            WorkOrder = Plate(
                "WORK ORDER 01 / FUEL CASK\n\n" +
                "SHIFT: 20 MIN     DOSE CEILING: 100\n" +
                "1  CREW AT HOIST + TRAVERSE. SPOTTER AT POOL.\n" +
                "2  ALIGN ASSEMBLY WITH CASK. LOWER CAREFULLY.\n" +
                "3  PARTNER HOLDS LID. TORQUE EACH TO 80 +/-5 NM.\n" +
                "   STAR ORDER: 1 5 3 7 2 6 4 8.\n" +
                "4  TWO HANDLERS MOVE CASK TO TRANSPORT BAY.\n" +
                "5  HOLD DISPATCH HANDLE. THREE CREW BELOW CEILING.\n\n" +
                "WASD MOVE / MOUSE LOOK / HOLD E OPERATE\n" +
                "Q + E REVERSE OR LOOSEN / F SUIT LAMP\n" +
                "TORQUE RETRY: BACK OFF ALL EIGHT TO ZERO.\n" +
                "R WORK ORDER / T WRIST / V TALK\n" +
                "LOCKOUT: 1-6 SELECT SUIT, HOLD E SIX SECONDS.\n" +
                "ONLY TWO LOCKOUTS. LOST HANDS STAY LOST.",
                new Vector3(-16.1f, 2.07f, -9.73f), Vector3.forward,
                new Vector2(6.4f, 3.5f), 0.117f, pale, dark.color);
            Target("clipboard", WorkOrder.transform.parent);
            Control("Shift start key", "start", new Vector3(-12.1f, 1.18f, -5.0f), Vector3.forward,
                "SHIFT START\nHOST / HOLD E", false);
            Control("Lockout lever", "lockout", new Vector3(-10.25f, 1.2f, -3.65f), Vector3.forward,
                "SUIT LOCKOUT\n1-6 SELECT / HOLD E\nTWO USES PER SHIFT", true);
            Report = Plate("SHIFT REPORT\nAWAITING DISPATCH", new Vector3(-19.72f, 2.1f, -6.5f), Vector3.right,
                new Vector2(4.8f, 3f), 0.145f, pale, dark.color);
            Box("Ready room bench", new Vector3(-16f, 0.48f, -3.95f), new Vector3(5f, 0.18f, 0.55f), steel);
            for (int i = 0; i < 2; i++)
                Box("Bench leg", new Vector3(-18f + i * 4f, 0.2f, -3.95f), new Vector3(0.1f, 0.4f, 0.45f), steel);
        }

        void BuildCrane()
        {
            Box("Pool lining", new Vector3(10f, 0.06f, 3f), new Vector3(4.2f, 0.12f, 4.2f), dark);
            Box("Pool surface / greybox", new Vector3(10f, 0.14f, 3f), new Vector3(3.6f, 0.04f, 3.6f), water, false);
            for (int i = -1; i <= 1; i += 2)
            {
                Box("Pool rim east-west", new Vector3(10f + i * 2f, 0.3f, 3f), new Vector3(0.4f, 0.6f, 4.4f), concrete);
                Box("Pool rim north-south", new Vector3(10f, 0.3f, 3f + i * 2f), new Vector3(4.4f, 0.6f, 0.4f), concrete);
                Box("Crane runway", new Vector3(11f, 5.85f, 3f + i * 3.4f), new Vector3(15f, 0.22f, 0.22f), steel);
            }
            Plate("POOL EDGE\nLIVE ASSEMBLY", new Vector3(10f, 0.6f, 0.77f), Vector3.back,
                new Vector2(2f, 0.55f), 0.1f, yellow, dark.color);
            var hookRoot = new GameObject("Crane hook / assembly carrier").transform;
            hookRoot.SetParent(transform, false);
            hookRoot.position = new Vector3(10f, 5.5f, 3f);
            CraneHook = hookRoot;
            Primitive("Hoist block", PrimitiveType.Cube, hookRoot.position,
                new Vector3(0.6f, 0.35f, 0.6f), yellow, false, hookRoot);
            Primitive("Fuel assembly", PrimitiveType.Cylinder, hookRoot.position + new Vector3(0f, -1.0f, 0f),
                new Vector3(0.4f, 0.78f, 0.4f), steel, false, hookRoot);
            craneBridge = Box("Crane bridge", new Vector3(10f, 6.03f, 3f), new Vector3(0.4f, 0.25f, 7.1f), steel, false);
            craneTrolley = Box("Crane trolley", new Vector3(10f, 5.82f, 3f), new Vector3(0.7f, 0.25f, 0.7f), dark, false);
            hoistCable = Primitive("Hoist cable", PrimitiveType.Cylinder, new Vector3(10f, 5.66f, 3f),
                new Vector3(0.035f, 0.16f, 0.035f), dark, false);

            Attenuate(Box("Hoist sight baffle", new Vector3(4f, 1.45f, -6.9f), new Vector3(3.6f, 2.9f, 0.35f), concrete), 0.06f);
            Attenuate(Box("Traverse sight baffle", new Vector3(16f, 1.45f, -6.9f), new Vector3(3.6f, 2.9f, 0.35f), concrete), 0.06f);
            Control("Hoist panel", "hoist", new Vector3(4f, 1.2f, -7.23f), Vector3.back,
                "HOIST\nE RAISE / Q+E LOWER\nTRAVERSE OPERATOR REQUIRED", false);
            Control("Traverse panel", "traverse", new Vector3(16f, 1.2f, -7.23f), Vector3.back,
                "TRAVERSE\nE FORWARD / Q+E REVERSE\nHOIST OPERATOR REQUIRED", false);
            Control("Lower hoist button", "crane_down", new Vector3(3.05f, 1.05f, -7.23f), Vector3.back,
                "LOWER", false, 0.55f);
            Control("Raise hoist button", "crane_up", new Vector3(4.95f, 1.05f, -7.23f), Vector3.back,
                "RAISE", false, 0.55f);
            CraneGauge = Plate("HOIST LOAD\n0000 KG", new Vector3(4f, 2.07f, -7.10f), Vector3.back,
                new Vector2(1.4f, 0.55f), 0.12f, dark, pale.color);
        }

        void BuildCask()
        {
            var caskRoot = new GameObject("Cask 01 / two-person handling").transform;
            caskRoot.SetParent(transform, false);
            caskRoot.position = new Vector3(8f, 0.85f, 0f);
            Primitive("Cask body", PrimitiveType.Cylinder, caskRoot.position,
                new Vector3(1.8f, 0.8f, 1.8f), steel, true, caskRoot);
            Primitive("Cask foot", PrimitiveType.Cylinder, caskRoot.position + Vector3.down * 0.76f,
                new Vector3(2.0f, 0.075f, 2.0f), dark, true, caskRoot);
            var lid = Primitive("Cask lid", PrimitiveType.Cylinder, caskRoot.position + Vector3.up * 0.83f,
                new Vector3(1.93f, 0.06f, 1.93f), lead, true, caskRoot);
            Target("lid", lid);
            var lidHandle = new GameObject("Lid holding handle").transform;
            lidHandle.position = caskRoot.position + new Vector3(0f, 0.99f, -0.94f);
            lidHandle.SetParent(lid, true);
            Box("Lid handle worn grip", lidHandle.position, new Vector3(0.56f, 0.13f, 0.13f), yellow, true, lidHandle);
            Target("lid", lidHandle);
            Bolts = new Transform[8];
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Bolts[i] = Primitive("Bolt " + (i + 1), PrimitiveType.Cylinder,
                    caskRoot.position + radial * 0.73f + Vector3.up * 0.975f,
                    new Vector3(0.17f, 0.095f, 0.17f), yellow, true, caskRoot);
                Target("bolt" + i, Bolts[i]);
                var number = Text((i + 1).ToString(), caskRoot.position + radial * 0.53f + Vector3.up * 0.895f,
                    Vector3.up, 0.08f, pale.color, caskRoot);
                number.alignment = TextAlignment.Center;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var handle = Box("Cask carrying handle", caskRoot.position + new Vector3(side * 1.04f, 0.1f, 0f),
                    new Vector3(0.14f, 0.14f, 0.85f), yellow, true, caskRoot);
                Target("cask", handle);
            }
            Plate("CASK 01\nTWO HANDLERS", caskRoot.position + new Vector3(0f, 0f, -0.91f), Vector3.back,
                new Vector2(1.15f, 0.55f), 0.09f, pale, dark.color, caskRoot);
            Cask = caskRoot.gameObject.AddComponent<Rigidbody>();
            ConfigureBody(Cask, 300f);
            Target("cask", caskRoot);
        }

        void BuildShield()
        {
            var shieldRoot = new GameObject("Movable lead shield").transform;
            shieldRoot.SetParent(transform, false);
            shieldRoot.position = new Vector3(5f, 1.1f, 0f);
            Box("Lead shield plate", shieldRoot.position + Vector3.up * 0.15f, new Vector3(0.25f, 2.2f, 2.5f), lead, true, shieldRoot);
            Box("Shield foot", shieldRoot.position + Vector3.down * 0.99f, new Vector3(1f, 0.2f, 2.7f), steel, true, shieldRoot);
            Box("Shield grab rail", shieldRoot.position + Vector3.left * 0.36f, new Vector3(0.12f, 0.15f, 1.7f), yellow, true, shieldRoot);
            Plate("LEAD SHIELD\nHOLD E TO MOVE", shieldRoot.position + Vector3.left * 0.16f + Vector3.up * 0.5f,
                Vector3.left, new Vector2(1.6f, 0.7f), 0.11f, pale, dark.color, shieldRoot);
            Attenuate(shieldRoot, 0.055f);
            Shield = shieldRoot.gameObject.AddComponent<Rigidbody>();
            ConfigureBody(Shield, 40f);
            Target("shield", shieldRoot);
        }

        void BuildServices()
        {
            Box("Ventilation duct", new Vector3(18.8f, 3.3f, 7.6f), new Vector3(2.2f, 2.2f, 1.1f), dark);
            var rotor = new GameObject("Vent fan rotor").transform;
            rotor.SetParent(transform, false);
            rotor.position = new Vector3(18.8f, 3.3f, 6.99f);
            VentFan = rotor;
            for (int i = 0; i < 4; i++)
            {
                var blade = Box("Fan blade", rotor.position, new Vector3(0.2f, 1.75f, 0.07f), steel, false, rotor);
                blade.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            }
            for (int i = -2; i <= 2; i++)
                Box("Fan guard", rotor.position + new Vector3(i * 0.33f, 0f, -0.1f), new Vector3(0.035f, 1.9f, 0.05f), dark, false);
            Control("Ventilation isolator", "vent", new Vector3(18.8f, 1.22f, 6.88f), Vector3.back,
                "VENTILATION\nE / Q ISOLATE\nKEEP RUNNING DURING SHIFT", true);
        }

        void ConfigureBody(Rigidbody body, float mass)
        {
            body.mass = mass;
            body.linearDamping = 4f;
            body.angularDamping = 8f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.maxAngularVelocity = 1.5f;
        }

        void BayLines(Vector3 center, float width, float depth)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Box("Transport bay paint", center + Vector3.right * s * width * 0.5f, new Vector3(0.1f, 0.015f, depth), pale, false);
                Box("Transport bay paint", center + Vector3.forward * s * depth * 0.5f, new Vector3(width, 0.015f, 0.1f), pale, false);
            }
        }

        Transform Control(string name, string id, Vector3 position, Vector3 normal, string label, bool warning, float scale = 1f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.position = position;
            root.rotation = Quaternion.LookRotation(normal, Vector3.up);
            var panel = Box(name + " enclosure", position, new Vector3(0.8f, 0.74f, 0.2f) * scale, steel, true, root);
            panel.localRotation = Quaternion.identity;
            float postHeight = Mathf.Max(0.1f, position.y - 0.37f * scale);
            Box(name + " pedestal", new Vector3(position.x, postHeight * 0.5f, position.z),
                new Vector3(0.09f, postHeight, 0.09f), steel, true, root);
            var handle = Box(name + " worn handle", position + normal * 0.19f,
                new Vector3(0.12f, 0.38f, 0.12f) * scale, warning ? red : yellow, true, root);
            handle.localRotation = Quaternion.identity;
            Plate(label, position + normal * 0.13f + Vector3.up * 0.59f * scale, normal,
                new Vector2(1.55f, 0.63f) * scale, 0.083f * scale, pale, dark.color, root);
            Target(id, root);
            return root;
        }

        void Target(string id, Transform target)
        {
            var marker = target.GetComponent<WorkTarget>();
            if (marker == null) marker = target.gameObject.AddComponent<WorkTarget>();
            marker.Id = id;
            if (!Targets.ContainsKey(id)) Targets.Add(id, target);
            if (id == "cask" && target.GetComponent<Rigidbody>() != null) Targets[id] = target;
        }

        void Attenuate(Transform target, float factor)
        {
            target.gameObject.AddComponent<ShieldMaterial>().Factor = factor;
        }

        void Lamp(Vector3 position)
        {
            var lamp = new GameObject("Sodium work light").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.position = position;
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.83f, 0.6f);
            lamp.intensity = 1.5f;
            lamp.range = 12f;
            lamp.shadows = LightShadows.Soft;
        }

        Material MakeMaterial(string name, Color color, float metallic = 0f)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            ownedMaterials.Add(material);
            return material;
        }

        Material MakeGlassMaterial()
        {
            var glass = MakeMaterial("Lead glass", new Color(0.38f, 0.41f, 0.36f, 0.18f));
            if (glass.HasProperty("_Mode"))
            {
                glass.SetFloat("_Mode", 2f);
                glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                glass.SetInt("_ZWrite", 0);
                glass.DisableKeyword("_ALPHATEST_ON");
                glass.EnableKeyword("_ALPHABLEND_ON");
                glass.renderQueue = 3000;
            }
            return glass;
        }

        Transform Box(string name, Vector3 position, Vector3 scale, Material material, bool collider = true, Transform parent = null)
            => Primitive(name, PrimitiveType.Cube, position, scale, material, collider, parent);

        Transform Primitive(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material,
            bool collider = true, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : transform, true);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider || shape == PrimitiveType.Cylinder)
            {
                var shapeCollider = go.GetComponent<Collider>();
                shapeCollider.enabled = false;
                Destroy(shapeCollider);
                if (collider)
                {
                    // Unity's cylinder primitive has a capsule collider. Flat cask feet and
                    // small bolt heads need their actual shape to remain on the floor.
                    var cylinderCollider = go.AddComponent<MeshCollider>();
                    cylinderCollider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                    cylinderCollider.convex = true;
                }
            }
            return go.transform;
        }

        TextMesh Plate(string text, Vector3 position, Vector3 normal, Vector2 dimensions, float characterSize,
            Material backing, Color ink, Transform parent = null)
        {
            var root = new GameObject("Plate / " + text.Split('\n')[0]).transform;
            root.SetParent(parent != null ? parent : transform, true);
            root.position = position;
            root.rotation = FaceNormal(normal);
            var plate = Box("Plate backing", position, new Vector3(dimensions.x, dimensions.y, 0.04f), backing, true, root);
            plate.localRotation = Quaternion.identity;
            var label = Text(text, position + normal * 0.026f, normal, characterSize, ink, root);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            return label;
        }

        TextMesh Text(string text, Vector3 position, Vector3 normal, float size, Color color, Transform parent)
        {
            var label = new GameObject("Engraved text").AddComponent<TextMesh>();
            label.transform.SetParent(parent, true);
            label.transform.position = position;
            label.transform.rotation = FaceNormal(normal);
            label.text = text;
            label.font = font;
            label.fontSize = 64;
            label.characterSize = size * 10f / label.fontSize;
            label.lineSpacing = 1.05f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return label;
        }

        static Quaternion FaceNormal(Vector3 normal)
            => Quaternion.LookRotation(-normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up);

        void LateUpdate()
        {
            if (CraneHook == null || craneBridge == null) return;
            var hook = CraneHook.position;
            craneBridge.position = new Vector3(hook.x, 6.03f, 3f);
            craneTrolley.position = new Vector3(hook.x, 5.82f, hook.z);
            float cableTop = 5.72f;
            float cableBottom = Mathf.Min(hook.y + 0.17f, cableTop);
            hoistCable.position = new Vector3(hook.x, (cableTop + cableBottom) * 0.5f, hook.z);
            hoistCable.localScale = new Vector3(0.035f, Mathf.Max(0.015f, (cableTop - cableBottom) * 0.5f), 0.035f);
        }

        void OnDestroy()
        {
            foreach (var material in ownedMaterials)
                if (material != null) Destroy(material);
        }
    }
}
