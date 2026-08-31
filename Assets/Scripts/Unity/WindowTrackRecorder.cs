using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Records every pilot's KiteStateView at a fixed rate and replays it.
    ///
    /// What is recorded is the model-independent view, not the model's own state,
    /// which is what lets a routine flown today be replayed against a future kite
    /// model, and what lets one recording drive every view at once. Playback pushes
    /// those frames back into WindowKiteBehaviour, so the meshes, the trails and the
    /// judge's framing follow a replay exactly as they follow live flight.
    ///
    /// The format is CSV and lives in Kitesim.Core (TrackCsv); this component only
    /// owns the file and the buttons.
    /// </summary>
    public class WindowTrackRecorder : MonoBehaviour
    {
        /// <summary>
        /// Samples per second. 50 Hz is the model's own step rate, so recording adds
        /// no smoothing of its own; a minute of four pilots is about 1.5 MB of CSV.
        /// </summary>
        [SerializeField] private double sampleRate = 50.0;

        /// <summary>
        /// Record and play. P, the obvious key for play, is already a flying control
        /// in the Player action map, so playback is on T.
        /// </summary>
        [SerializeField] private Key recordKey = Key.R;
        [SerializeField] private Key playKey = Key.T;

        [SerializeField] private string fileName = "track.csv";

        private readonly List<WindowKiteBehaviour> kites = new List<WindowKiteBehaviour>();
        private readonly Track track = new Track();

        private double time;
        private double nextSample;

        public bool IsRecording { get; private set; }
        public bool IsPlaying { get; private set; }
        public Track Track => track;

        /// <summary>Where Save writes and Load reads. Logged on every save.</summary>
        public string FilePath => Path.Combine(Application.persistentDataPath, fileName);

        public void Register(WindowKiteBehaviour kite)
        {
            kites.Add(kite);
        }

        public void StartRecording()
        {
            StopPlayback();
            track.Clear();
            time = 0.0;
            nextSample = 0.0;
            IsRecording = true;
        }

        public void StopRecording()
        {
            IsRecording = false;
        }

        public void StartPlayback()
        {
            if (track.Frames.Count == 0)
                return;

            IsRecording = false;
            IsPlaying = true;
            time = 0.0;
        }

        /// <summary>
        /// Stop replaying and hand every kite back to the live model. Kites are
        /// resumed rather than left holding their last replayed frame, otherwise a
        /// stopped playback would look like a frozen simulation.
        /// </summary>
        public void StopPlayback()
        {
            if (!IsPlaying)
                return;

            IsPlaying = false;
            for (int i = 0; i < kites.Count; i++)
                kites[i].Resume();
        }

        public void Save()
        {
            File.WriteAllText(FilePath, TrackCsv.Write(track));
            Debug.Log($"[Track] saved {track.Frames.Count} frames to {FilePath}");
        }

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                Debug.LogWarning($"[Track] nothing to load at {FilePath}");
                return;
            }

            StopPlayback();
            StopRecording();
            TrackCsv.ReadInto(track, File.ReadAllText(FilePath));
            Debug.Log($"[Track] loaded {track.Frames.Count} frames from {FilePath}");
        }

        void Update()
        {
            ReadKeys();

            if (IsRecording)
            {
                RecordFrames();
                return;
            }

            if (IsPlaying)
                ReplayFrames();
        }

        private void RecordFrames()
        {
            time += Time.deltaTime;
            if (time < nextSample)
                return;

            for (int i = 0; i < kites.Count; i++)
                track.Record(time, kites[i].PilotIndex, kites[i].View);

            // Advance by whole sample periods rather than to the current time, so a
            // frame that arrives late does not shift the whole recording after it.
            do
            {
                nextSample += 1.0 / sampleRate;
            }
            while (nextSample <= time);
        }

        private void ReplayFrames()
        {
            time += Time.deltaTime;
            if (time > track.Duration)
                time = 0.0;

            for (int i = 0; i < kites.Count; i++)
            {
                double lineLength = kites[i].Sim != null ? kites[i].Sim.Config.LineLength : 45.0;
                kites[i].ShowPlaybackFrame(track.Sample(time, kites[i].PilotIndex, lineLength));
            }
        }

        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard[recordKey].wasPressedThisFrame)
            {
                if (IsRecording)
                    StopRecording();
                else
                    StartRecording();
            }

            if (keyboard[playKey].wasPressedThisFrame)
            {
                if (IsPlaying)
                    StopPlayback();
                else
                    StartPlayback();
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 190, 178), GUI.skin.box);

            GUILayout.Label(IsRecording ? "REC" : IsPlaying ? "PLAY" : "IDLE");
            GUILayout.Label($"frames: {track.Frames.Count}");
            GUILayout.Label($"{track.Duration:F1} s");

            if (GUILayout.Button(IsRecording ? "Stop recording (R)" : "Record (R)"))
            {
                if (IsRecording)
                    StopRecording();
                else
                    StartRecording();
            }

            GUI.enabled = track.Frames.Count > 0 && !IsRecording;
            if (GUILayout.Button(IsPlaying ? "Stop playback (T)" : "Play (T)"))
            {
                if (IsPlaying)
                    StopPlayback();
                else
                    StartPlayback();
            }

            if (GUILayout.Button("Save"))
                Save();
            GUI.enabled = true;

            if (GUILayout.Button("Load"))
                Load();

            GUILayout.EndArea();
        }
    }
}
