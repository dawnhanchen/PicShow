using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PicShow.PicoInteraction
{
    // Keeps the mode panel loosely attached to the head for a short onboarding
    // window, then locks it in world space so it does not chase the user forever.
    public sealed class TimedLazyFollow : MonoBehaviour
    {
        [SerializeField] private LazyFollow lazyFollow;
        [SerializeField] private float followSeconds = 3f;

        private bool running;
        private bool started;
        private float startedAt;

        public float FollowSeconds => followSeconds;
        public bool Following => running && lazyFollow != null && lazyFollow.enabled;
        public bool WindowStarted => started;

        public void Configure(LazyFollow follow, float seconds)
        {
            lazyFollow = follow;
            followSeconds = Mathf.Max(0f, seconds);
        }

        private void Start()
        {
            if (lazyFollow == null)
                lazyFollow = GetComponent<LazyFollow>();
            if (lazyFollow == null || !lazyFollow.enabled)
                return;
            running = true;
        }

        private void Update()
        {
            if (!running)
                return;
            if (!started)
            {
                started = true;
                startedAt = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - startedAt < followSeconds)
                return;
            running = false;
            if (lazyFollow != null)
                lazyFollow.enabled = false;
        }

        private void OnDisable()
        {
            running = false;
            started = false;
            // Hiding the panel ends the onboarding follow permanently, so a later
            // return to the mode panel does not start chasing the head again.
            if (lazyFollow != null)
                lazyFollow.enabled = false;
        }
    }
}
