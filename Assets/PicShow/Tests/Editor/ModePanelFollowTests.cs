using System.Reflection;
using NUnit.Framework;
using PicShow.PicoInteraction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PicShow.Tests
{
    public class ModePanelFollowTests
    {
        [Test]
        public void HidingModePanelEndsFollowPermanently()
        {
            var panel = new GameObject("Follow panel");
            try
            {
                var follow = panel.AddComponent<LazyFollow>();
                var timed = panel.AddComponent<TimedLazyFollow>();
                timed.Configure(follow, 3f);
                Assert.That(timed.FollowSeconds, Is.EqualTo(3f).Within(0.01f));
                Assert.That(follow.enabled, Is.True);

                typeof(TimedLazyFollow)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(timed, null);

                Assert.That(follow.enabled, Is.False,
                    "Leaving the mode panel must not restart head following on return.");
                Assert.That(timed.Following, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(panel);
            }
        }
    }
}
