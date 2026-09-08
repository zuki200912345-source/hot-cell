using UnityEngine;

namespace HotCell
{
    public sealed class CrewRig : MonoBehaviour
    {
        public int Slot;
        public CharacterController Controller { get; private set; }
        public Transform Head { get; private set; }
        public Light Lamp { get; private set; }
        public Vector3 TargetPosition;
        public float TargetYaw;
        private Renderer bodyRenderer, visorRenderer;
        private TextMesh number;

        public static CrewRig Create(int slot)
        {
            GameObject root = new GameObject("Suit " + (slot + 1).ToString("00"));
            CrewRig rig = root.AddComponent<CrewRig>();
            rig.Slot = slot;
            rig.Controller = root.AddComponent<CharacterController>();
            rig.Controller.height = 1.8f;
            rig.Controller.radius = .3f;
            rig.Controller.center = new Vector3(0, .9f, 0);
            rig.Controller.stepOffset = .25f;
            rig.Controller.skinWidth = .03f;
            WorkTarget marker = root.AddComponent<WorkTarget>();
            marker.Id = "suit" + slot;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Sealed suit";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0, .9f, 0);
            body.transform.localScale = new Vector3(.58f, .9f, .58f);
            Destroy(body.GetComponent<Collider>());
            rig.bodyRenderer = body.GetComponent<Renderer>();
            rig.bodyRenderer.material.color = new Color(.49f, .48f, .42f);
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.transform.SetParent(root.transform, false);
            visor.transform.localPosition = new Vector3(0, 1.48f, .24f);
            visor.transform.localScale = new Vector3(.4f, .17f, .11f);
            Destroy(visor.GetComponent<Collider>());
            rig.visorRenderer = visor.GetComponent<Renderer>();
            rig.visorRenderer.material.color = new Color(.08f, .08f, .075f);
            rig.Head = new GameObject("Head").transform;
            rig.Head.SetParent(root.transform, false);
            rig.Head.localPosition = Vector3.up * 1.55f;
            GameObject lamp = new GameObject("Suit lamp");
            lamp.transform.SetParent(rig.Head, false);
            lamp.transform.localPosition = new Vector3(.17f, .1f, .32f);
            rig.Lamp = lamp.AddComponent<Light>();
            rig.Lamp.type = LightType.Spot;
            rig.Lamp.range = 17;
            rig.Lamp.spotAngle = 53;
            rig.Lamp.intensity = 2.5f;
            rig.Lamp.color = new Color(1, .92f, .75f);
            rig.Lamp.shadows = LightShadows.Hard;
            GameObject stencil = new GameObject("Prototype number");
            stencil.transform.SetParent(root.transform, false);
            stencil.transform.localPosition = Vector3.up * 2.05f;
            rig.number = stencil.AddComponent<TextMesh>();
            rig.number.text = (slot + 1).ToString("00");
            WorldText.Configure(rig.number, .2f, new Color(.85f, .84f, .76f));
            rig.number.anchor = TextAnchor.MiddleCenter;
            rig.number.color = new Color(.85f, .84f, .76f);
            root.SetActive(false);
            return rig;
        }

        public void MakeLocal(bool local)
        {
            bodyRenderer.enabled = !local;
            visorRenderer.enabled = !local;
            number.gameObject.SetActive(!local);
        }

        public void Teleport(Vector3 point)
        {
            Controller.enabled = false;
            transform.position = point;
            TargetPosition = point;
            Controller.enabled = true;
        }

        public void Move(InputFrame input, float dt, float speed)
        {
            transform.rotation = Quaternion.Euler(0, input.yaw, 0);
            Vector3 movement = new Vector3(input.horizontal, 0, input.vertical);
            movement = Vector3.ClampMagnitude(movement, 1);
            Controller.Move((transform.TransformDirection(movement) * speed + Vector3.down * 3) * dt);
            Head.localRotation = Quaternion.Euler(input.pitch, 0, 0);
            Lamp.enabled = input.lamp;
        }

        public void Interpolate(float dt)
        {
            Controller.enabled = false;
            transform.position = Vector3.Lerp(transform.position, TargetPosition, 1 - Mathf.Exp(-18 * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, TargetYaw, 0), 1 - Mathf.Exp(-18 * dt));
            Controller.enabled = true;
            if (Camera.main != null)
                number.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
