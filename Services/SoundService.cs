using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows.Threading;

namespace RightDock.Services
{
    internal sealed class SoundService : IDisposable
    {
        private const int RingSpacingMs = 850;

        private readonly AppSettings _settings;
        private readonly Dictionary<string, SoundPlayer> _players =
            new Dictionary<string, SoundPlayer>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MemoryStream> _streams = new List<MemoryStream>();
        private DispatcherTimer _ringTimer;
        private string _ringKey;
        private int _ringsLeft;
        private bool _ringForce;
        private bool _disposed;

        internal SoundService(AppSettings settings)
        {
            _settings = settings;
        }

        internal static IReadOnlyList<SoundOption> Options => BellSynth.Catalog;

        internal void PlayFocusComplete()
        {
            PlayRings(Resolve(FocusKey, BellSynth.DefaultFocus), 3, "focus session complete", false);
        }

        internal void PlayReminder()
        {
            PlayRings(Resolve(ReminderKey, BellSynth.DefaultReminder), 1, "reminder", false);
        }

        internal void PreviewFocus(string key)
        {
            PlayRings(Resolve(key, BellSynth.DefaultFocus), 3, "focus preview", true);
        }

        internal void PreviewReminder(string key)
        {
            PlayRings(Resolve(key, BellSynth.DefaultReminder), 1, "reminder preview", true);
        }

        private string FocusKey => _settings == null ? null : _settings.FocusSound;

        private string ReminderKey => _settings == null ? null : _settings.ReminderSound;

        private static string Resolve(string key, string fallback)
        {
            return BellSynth.IsKnown(key) ? key : fallback;
        }

        private bool Enabled => !_disposed && _settings != null && _settings.NotificationsEnabled;

        private void PlayRings(string key, int rings, string label, bool force)
        {
            if (_disposed) return;
            if (!force && !Enabled) return;

            CancelRings();
            _ringKey = key;
            _ringsLeft = Math.Max(1, rings);
            _ringForce = force;
            Log.Info("Bells: " + label + " using \"" + key + "\" x" + _ringsLeft + ".");

            RingOnce();
            if (_ringsLeft <= 0) return;

            _ringTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(RingSpacingMs),
            };
            _ringTimer.Tick += OnRingTick;
            _ringTimer.Start();
        }

        private void OnRingTick(object sender, EventArgs e)
        {
            if (!_ringForce && !Enabled)
            {
                CancelRings();
                return;
            }

            RingOnce();
            if (_ringsLeft <= 0) CancelRings();
        }

        private void RingOnce()
        {
            _ringsLeft--;

            SoundPlayer player = GetPlayer(_ringKey);
            if (player == null) return;

            try
            {
                player.Play();
            }
            catch (Exception ex)
            {
                Log.Error("Could not play the bell \"" + _ringKey + "\".", ex);
                CancelRings();
            }
        }

        private SoundPlayer GetPlayer(string key)
        {
            if (_players.TryGetValue(key, out SoundPlayer cached)) return cached;

            try
            {
                var stream = new MemoryStream(BellSynth.CreateWav(key), false);
                var player = new SoundPlayer(stream);
                player.Load();

                _players[key] = player;
                _streams.Add(stream);
                return player;
            }
            catch (Exception ex)
            {
                Log.Error("Could not prepare the bell \"" + key + "\".", ex);
                return null;
            }
        }

        private void CancelRings()
        {
            if (_ringTimer != null)
            {
                _ringTimer.Stop();
                _ringTimer.Tick -= OnRingTick;
                _ringTimer = null;
            }

            _ringsLeft = 0;
            _ringForce = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CancelRings();

            foreach (SoundPlayer player in _players.Values)
            {
                try
                {
                    player.Stop();
                    player.Dispose();
                }
                catch
                {
                    // Shutdown cleanup is best-effort.
                }
            }

            _players.Clear();

            foreach (MemoryStream stream in _streams)
            {
                stream.Dispose();
            }

            _streams.Clear();
        }
    }
}