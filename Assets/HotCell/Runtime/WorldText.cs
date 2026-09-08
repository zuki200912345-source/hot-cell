using UnityEngine;

namespace HotCell
{
    /// <summary>Shared font atlas with world occlusion and metre-based character sizing.</summary>
    public static class WorldText
    {
        static Font font;
        static Material material;

        public static void Configure(TextMesh text, float emHeight, Color color)
        {
            EnsureMaterial();
            text.font = font;
            text.fontSize = 64;
            text.characterSize = emHeight * 10f / text.fontSize;
            text.color = color;
            text.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void EnsureMaterial()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (material != null) return;
            var shader = Resources.Load<Shader>("HotCellWorldText");
            if (shader == null) shader = Shader.Find("HotCell/World Text");
            if (shader == null)
            {
                Debug.LogError("HOT CELL world text shader is missing; check Assets/HotCell/Resources.");
                shader = font.material.shader;
            }
            material = new Material(shader) { name = "HOT CELL world text", mainTexture = font.material.mainTexture };
            Font.textureRebuilt += UpdateAtlas;
        }

        static void UpdateAtlas(Font rebuiltFont)
        {
            if (rebuiltFont == font && material != null)
                material.mainTexture = font.material.mainTexture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            Font.textureRebuilt -= UpdateAtlas;
            if (material != null) Object.Destroy(material);
            material = null;
            font = null;
        }
    }
}
