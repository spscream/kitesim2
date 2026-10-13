using Kitesim.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Kitesim.Unity.EditorTests
{
    public class WindowControlsHelpTests
    {
        [Test]
        public void CopyNamesKeyboardAndGamepadFlightBindings()
        {
            StringAssert.Contains("A/D", WindowControlsHelp.Copy);
            StringAssert.Contains("O/P", WindowControlsHelp.Copy);
            StringAssert.Contains("W/S", WindowControlsHelp.Copy);
            StringAssert.Contains("Space", WindowControlsHelp.Copy);
            StringAssert.Contains("stick", WindowControlsHelp.Copy.ToLowerInvariant());
            StringAssert.Contains("LT/RT", WindowControlsHelp.Copy);
            StringAssert.Contains("A / Start", WindowControlsHelp.Copy);
        }

        [Test]
        public void CopyOmitsSessionKeys()
        {
            string copy = WindowControlsHelp.Copy;

            StringAssert.DoesNotContain(" view", copy);
            StringAssert.DoesNotContain("(V)", copy);
            StringAssert.DoesNotContain("(C)", copy);
            StringAssert.DoesNotContain("(N)", copy);
            StringAssert.DoesNotContain("(R)", copy);
            StringAssert.DoesNotContain("(T)", copy);
            StringAssert.DoesNotContain("(G)", copy);
        }

        [Test]
        public void QuestionMarkTogglesThePinnedCard()
        {
            var go = new GameObject("help");
            try
            {
                var help = go.AddComponent<WindowControlsHelp>();
                Assert.IsFalse(help.Pinned);

                help.TogglePinned();
                Assert.IsTrue(help.Pinned);

                help.TogglePinned();
                Assert.IsFalse(help.Pinned);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
