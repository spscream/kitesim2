using System.Collections;
using Kitesim.Core;
using Kitesim.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kitesim.Unity.PlayModeTests
{
    public class WindowTrackLifecycleTests
    {
        [UnityTest]
        public IEnumerator RecordedViewReplaysThroughTheSameBehaviour()
        {
            var kiteGo = new GameObject("Kite");
            var kite = kiteGo.AddComponent<WindowKiteBehaviour>();
            kite.Configure(0, Vec3.Zero, new WindField { Speed = 6.0, Direction = Vec3.Forward });
            kite.Launch();
            kite.SetInputs(-1f, 1f, 0f);

            var recorderGo = new GameObject("Recorder");
            var recorder = recorderGo.AddComponent<WindowTrackRecorder>();
            recorder.Register(kite);
            recorder.StartRecording();

            yield return new WaitForSeconds(0.25f);

            recorder.StopRecording();
            Assert.Greater(recorder.Track.Frames.Count, 4, "fixed-step recording must collect frames");

            KiteStateView first = recorder.Track.Sample(0.0, 0, 45.0);
            recorder.StartPlayback();
            yield return null;

            Assert.AreEqual(first.Speed, kite.View.Speed, 1e-6);
            Assert.AreEqual(first.Direction.X, kite.View.Direction.X, 1e-6);
            Assert.AreEqual(first.Direction.Y, kite.View.Direction.Y, 1e-6);
            Assert.AreEqual(first.Direction.Z, kite.View.Direction.Z, 1e-6);

            Object.Destroy(kiteGo);
            Object.Destroy(recorderGo);
        }
    }
}
