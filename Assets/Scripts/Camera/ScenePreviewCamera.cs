using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public sealed class ScenePreviewCamera : MonoBehaviour
    {
        [SerializeField] private AudioListener audioListener;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (audioListener == null)
                audioListener = GetComponent<AudioListener>();
        }

        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return;

            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayer == null)
                return;

            if (_camera != null)
                _camera.enabled = false;

            if (audioListener != null)
                audioListener.enabled = false;

            enabled = false;
        }
    }
}
