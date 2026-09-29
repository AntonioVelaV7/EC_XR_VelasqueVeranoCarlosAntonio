using System.Collections.Generic;
using UnityEngine;

namespace XRProject
{
    /// <summary>
    /// Manages room illumination including directional light and interior lights.
    /// Provides toggle functionality accessible via VR UI buttons.
    /// </summary>
    public class RoomLightingController : MonoBehaviour
    {
        [Header("Main Lights")]
        [Tooltip("The Directional Light of the scene.")]
        [SerializeField] private Light directionalLight;

        [Tooltip("Container GameObject holding all interior ceiling/room lights.")]
        [SerializeField] private GameObject interiorLightsGroup;

        [Header("State")]
        [SerializeField] private bool isLightingOn = true;

        public bool IsLightingOn => isLightingOn;

        private void Start()
        {
            if (directionalLight == null)
            {
                var dirLightObj = GameObject.Find("Directional Light");
                if (dirLightObj != null)
                {
                    directionalLight = dirLightObj.GetComponent<Light>();
                }
            }

            ApplyLightingState();
        }

        /// <summary>
        /// Toggles the lighting on and off.
        /// </summary>
        public void ToggleLighting()
        {
            isLightingOn = !isLightingOn;
            ApplyLightingState();
        }

        /// <summary>
        /// Sets the lighting state explicitly.
        /// </summary>
        public void SetLighting(bool state)
        {
            isLightingOn = state;
            ApplyLightingState();
        }

        private void ApplyLightingState()
        {
            if (directionalLight != null)
            {
                directionalLight.gameObject.SetActive(isLightingOn);
            }

            if (interiorLightsGroup != null)
            {
                interiorLightsGroup.SetActive(isLightingOn);
            }
        }
    }
}
