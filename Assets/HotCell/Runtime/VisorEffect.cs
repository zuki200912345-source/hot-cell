using UnityEngine;

namespace HotCell
{
    /// <summary>Private, restrained radiation symptoms for the local camera.</summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class VisorEffect : MonoBehaviour
    {
        [Range(0f, 1f)] public float DoseFraction;
        [SerializeField] private Shader visorShader;
        private Material visorMaterial;
        private static readonly int SnowId = Shader.PropertyToID("_Snow");
        private static readonly int DesaturationId = Shader.PropertyToID("_Desaturation");
        private static readonly int NoiseTimeId = Shader.PropertyToID("_NoiseTime");

        private void OnEnable()
        {
            if (visorShader == null) visorShader = Resources.Load<Shader>("Visor");
            if (visorShader == null) visorShader = Shader.Find("Hidden/HotCell/Visor");
            if (visorShader != null && visorShader.isSupported)
                visorMaterial = new Material(visorShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        // Built-in render pipeline image effect; the project does not enable an SRP.
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            float dose = float.IsNaN(DoseFraction) || float.IsInfinity(DoseFraction)
                ? 0f : Mathf.Clamp01(DoseFraction);
            if (visorMaterial == null || dose <= 0.25f)
            {
                Graphics.Blit(source, destination);
                return;
            }
            visorMaterial.SetFloat(SnowId, Mathf.InverseLerp(0.25f, 0.75f, dose) * 0.035f);
            visorMaterial.SetFloat(DesaturationId, Mathf.InverseLerp(0.75f, 1f, dose) * 0.85f);
            visorMaterial.SetFloat(NoiseTimeId, Time.unscaledTime * 1.3f);
            Graphics.Blit(source, destination, visorMaterial);
        }

        private void OnDisable()
        {
            if (visorMaterial == null) return;
            Destroy(visorMaterial);
            visorMaterial = null;
        }
    }
}
