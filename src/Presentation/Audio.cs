using System;
using System.Collections.Generic;
using Godot;

namespace LandBuilder.Presentation;

public enum Sfx
{
    Place,
    PlaceWater,
    Rotate,
    Invalid,
    Perfect,
    Flawless,
    QuestNew,
    QuestDone,
    QuestFail,
    Milestone,
    StreakUp,
    StreakLost,
    UiHover,
    UiClick,
    Undo,
    GameOver,
    Whoosh
}

/// <summary>
/// Every sound in the game is synthesised at start-up: no audio assets to ship. Placement notes climb a pentatonic
/// scale so chains of matches sound musical, and a soft generative pad plays underneath.
/// </summary>
public partial class Audio : Node
{
    private const int Rate = 32000;
    private static readonly float[] Pentatonic = [0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24];

    private readonly Dictionary<Sfx, AudioStreamWav[]> _sfx = new();
    private readonly List<AudioStreamWav> _notes = new();
    private readonly List<AudioStreamWav> _pads = new();
    private readonly List<AudioStreamPlayer> _sfxPlayers = new();
    private readonly List<AudioStreamPlayer> _musicPlayers = new();
    private AudioStreamPlayer _ambient = null!;
    private int _nextSfx;
    private int _nextMusic;
    private float _musicTimer = 1.5f;
    private int _chordIndex;
    private float _melodyTimer = 3f;
    private readonly Random _random = new(7);

    public override void _Ready()
    {
        EnsureBus("Music", -9f, reverb: true);
        EnsureBus("Sfx", -2f, reverb: true);
        EnsureBus("Ambient", -14f, reverb: false);

        for (var i = 0; i < 16; i++)
        {
            var p = new AudioStreamPlayer { Bus = "Sfx" };
            AddChild(p);
            _sfxPlayers.Add(p);
        }

        for (var i = 0; i < 6; i++)
        {
            var p = new AudioStreamPlayer { Bus = "Music" };
            AddChild(p);
            _musicPlayers.Add(p);
        }

        _ambient = new AudioStreamPlayer { Bus = "Ambient", Stream = Ocean() };
        AddChild(_ambient);

        Build();
    }

    public void StartAmbience()
    {
        if (!_ambient.Playing)
            _ambient.Play();
    }

    private static void EnsureBus(string name, float volumeDb, bool reverb)
    {
        if (AudioServer.GetBusIndex(name) >= 0)
            return;
        AudioServer.AddBus();
        var index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name);
        AudioServer.SetBusSend(index, "Master");
        AudioServer.SetBusVolumeDb(index, volumeDb);
        if (reverb)
        {
            AudioServer.AddBusEffect(index, new AudioEffectReverb { RoomSize = 0.55f, Damping = 0.6f, Wet = 0.18f, Dry = 1f, Spread = 0.8f });
        }
    }

    public static void SetBusLinear(string bus, float linear)
    {
        var index = AudioServer.GetBusIndex(bus);
        if (index < 0)
            return;
        AudioServer.SetBusMute(index, linear <= 0.001f);
        var baseDb = bus switch { "Music" => -9f, "Sfx" => -2f, "Ambient" => -14f, _ => 0f };
        AudioServer.SetBusVolumeDb(index, baseDb + Mathf.LinearToDb(Mathf.Max(linear, 0.001f)));
    }

    // ------------------------------------------------------------------ playback

    public void Play(Sfx sfx, float pitch = 1f, float volumeDb = 0f)
    {
        if (!_sfx.TryGetValue(sfx, out var variants))
            return;
        var player = _sfxPlayers[_nextSfx];
        _nextSfx = (_nextSfx + 1) % _sfxPlayers.Count;
        player.Stream = variants[_random.Next(variants.Length)];
        player.PitchScale = pitch * (1f + (((float)_random.NextDouble() - 0.5f) * 0.04f));
        player.VolumeDb = volumeDb;
        player.Play();
    }

    /// <summary>Plays the n-th note of the pentatonic ladder; used for each matched edge.</summary>
    public void PlayNote(int step, float volumeDb = -4f)
    {
        var index = Math.Clamp(step, 0, _notes.Count - 1);
        var player = _sfxPlayers[_nextSfx];
        _nextSfx = (_nextSfx + 1) % _sfxPlayers.Count;
        player.Stream = _notes[index];
        player.PitchScale = 1f;
        player.VolumeDb = volumeDb;
        player.Play();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_pads.Count == 0)
            return;

        _musicTimer -= dt;
        if (_musicTimer <= 0f)
        {
            _musicTimer = 7.5f;
            PlayMusic(_pads[_chordIndex % _pads.Count], -6f);
            _chordIndex++;
        }

        _melodyTimer -= dt;
        if (_melodyTimer <= 0f)
        {
            _melodyTimer = 1.2f + ((float)_random.NextDouble() * 3.5f);
            if (_random.NextDouble() < 0.55)
            {
                var step = _random.Next(3, 9);
                PlayMusic(_notes[step], -16f);
            }
        }
    }

    private void PlayMusic(AudioStream stream, float volumeDb)
    {
        var player = _musicPlayers[_nextMusic];
        _nextMusic = (_nextMusic + 1) % _musicPlayers.Count;
        player.Stream = stream;
        player.VolumeDb = volumeDb;
        player.Play();
    }

    // ------------------------------------------------------------------ synthesis

    private void Build()
    {
        _sfx[Sfx.Place] = [Thock(1f, 0), Thock(0.93f, 1), Thock(1.07f, 2)];
        _sfx[Sfx.PlaceWater] = [Plop(1f, 3), Plop(1.1f, 4)];
        _sfx[Sfx.Rotate] = [Tick(2400f, 5), Tick(2100f, 6)];
        _sfx[Sfx.Invalid] = [Bonk()];
        _sfx[Sfx.Perfect] = [Chime([0, 4, 7, 12], 0.07f, 1.6f, 0.45f)];
        _sfx[Sfx.Flawless] = [Chime([0, 4, 7, 12, 16, 19, 24], 0.06f, 2.2f, 0.5f)];
        _sfx[Sfx.QuestNew] = [Chime([7, 12], 0.09f, 0.8f, 0.3f)];
        _sfx[Sfx.QuestDone] = [Fanfare([0, 4, 7, 12], [0, 4, 7, 12, 16])];
        _sfx[Sfx.QuestFail] = [Chime([7, 3, 0], 0.14f, 0.9f, 0.25f, wave: 1)];
        _sfx[Sfx.Milestone] = [Fanfare([0, 7, 12, 16, 19], [0, 7, 12, 16, 19, 24])];
        _sfx[Sfx.StreakUp] = [Sweep(500f, 1100f, 0.18f, 0.18f)];
        _sfx[Sfx.StreakLost] = [Sweep(500f, 260f, 0.25f, 0.16f)];
        _sfx[Sfx.UiHover] = [Tick(3200f, 9, 0.05f)];
        _sfx[Sfx.UiClick] = [Pop()];
        _sfx[Sfx.Undo] = [Sweep(900f, 420f, 0.2f, 0.2f)];
        _sfx[Sfx.GameOver] = [Chime([12, 7, 4, 0, -5], 0.22f, 2.5f, 0.35f)];
        _sfx[Sfx.Whoosh] = [Whoosh()];

        for (var i = 0; i < Pentatonic.Length; i++)
            _notes.Add(Marimba(Freq(Pentatonic[i]), 1.3f));

        // C - Am - F - G style progression voiced as soft pads (semitones from C4).
        int[][] chords = [[0, 4, 7, 11], [-3, 0, 4, 7], [-7, -3, 0, 5], [-5, -1, 2, 7], [0, 4, 7, 14], [-8, -5, 0, 4]];
        foreach (var chord in chords)
            _pads.Add(Pad(chord, 8.5f));
    }

    private static float Freq(float semitonesFromC5) => 523.25f * Mathf.Pow(2f, semitonesFromC5 / 12f);

    private static float FreqC4(float semitones) => 261.63f * Mathf.Pow(2f, semitones / 12f);

    private static AudioStreamWav ToWav(float[] samples, bool loop = false)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var v = (short)Math.Clamp((int)(samples[i] * 32767f), short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[(i * 2) + 1] = (byte)((v >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = bytes
        };
        if (loop)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = 0;
            wav.LoopEnd = samples.Length;
        }

        return wav;
    }

    private static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

    private static void Normalize(float[] s, float peak)
    {
        var max = 0.0001f;
        foreach (var v in s)
            max = Math.Max(max, Math.Abs(v));
        var k = peak / max;
        for (var i = 0; i < s.Length; i++)
            s[i] *= k;
    }

    /// <summary>Wooden tile set down on wood: pitched body thump plus a filtered click.</summary>
    private static AudioStreamWav Thock(float pitch, int seed)
    {
        var s = Buffer(0.35f);
        var rng = new Random(seed);
        var lp = 0f;
        var phase = 0.0;
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)Rate;
            var f = (150f * pitch * Mathf.Exp(-t * 18f)) + (78f * pitch);
            phase += Math.Tau * f / Rate;
            var body = (float)Math.Sin(phase) * Mathf.Exp(-t * 16f);
            var wood = (float)Math.Sin(phase * 3.1) * Mathf.Exp(-t * 45f) * 0.35f;
            var noise = ((float)rng.NextDouble() * 2f) - 1f;
            lp += (noise - lp) * 0.25f;
            var click = lp * Mathf.Exp(-t * 90f) * 0.9f;
            s[i] = body + wood + click;
        }

        Normalize(s, 0.9f);
        return ToWav(s);
    }

    private static AudioStreamWav Plop(float pitch, int seed)
    {
        var s = Buffer(0.4f);
        var rng = new Random(seed);
        var phase = 0.0;
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)Rate;
            var f = (380f + (900f * t)) * pitch;
            phase += Math.Tau * f / Rate;
            var bubble = (float)Math.Sin(phase) * Mathf.Exp(-t * 14f);
            var splash = (((float)rng.NextDouble() * 2f) - 1f) * Mathf.Exp(-t * 30f) * 0.25f;
            var thump = (float)Math.Sin(Math.Tau * 90 * pitch * t) * Mathf.Exp(-t * 20f) * 0.8f;
            s[i] = bubble * 0.6f + splash + thump;
        }

        Normalize(s, 0.85f);
        return ToWav(s);
    }

    private static AudioStreamWav Tick(float freq, int seed, float length = 0.06f)
    {
        var s = Buffer(length);
        var rng = new Random(seed);
        var bp = 0f;
        var lp = 0f;
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)Rate;
            var noise = ((float)rng.NextDouble() * 2f) - 1f;
            lp += (noise - lp) * 0.5f;
            bp = lp - bp * 0.2f;
            var tone = (float)Math.Sin(Math.Tau * freq * t) * 0.5f;
            s[i] = (bp * 0.5f + tone) * Mathf.Exp(-t * 70f);
        }

        Normalize(s, 0.5f);
        return ToWav(s);
    }

    private static AudioStreamWav Pop()
    {
        var s = Buffer(0.12f);
        var phase = 0.0;
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)Rate;
            var f = 900f - (t * 3000f);
            phase += Math.Tau * Math.Max(200f, f) / Rate;
            s[i] = (float)Math.Sin(phase) * Mathf.Exp(-t * 35f);
        }

        Normalize(s, 0.55f);
        return ToWav(s);
    }

    private static AudioStreamWav Bonk()
    {
        var s = Buffer(0.25f);
        var phase = 0.0;
        var lp = 0f;
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)Rate;
            phase += Math.Tau * (190f - (t * 160f)) / Rate;
            var square = Math.Sin(phase) > 0 ? 1f : -1f;
            lp += (square - lp) * 0.08f;
            s[i] = lp * Mathf.Exp(-t * 14f);
        }

        Normalize(s, 0.45f);
        return ToWav(s);
    }

    private static AudioStreamWav Sweep(float from, float to, float length, float peak)
    {
        var s = Buffer(length);
        var phase = 0.0;
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)s.Length;
            var f = Mathf.Lerp(from, to, Ease.OutCubic(t));
            phase += Math.Tau * f / Rate;
            var env = Mathf.Sin(Mathf.Pi * t);
            s[i] = ((float)Math.Sin(phase) + ((float)Math.Sin(phase * 2) * 0.3f)) * env;
        }

        Normalize(s, peak);
        return ToWav(s);
    }

    private static AudioStreamWav Whoosh()
    {
        var s = Buffer(0.45f);
        var rng = new Random(77);
        var lp = 0f;
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)s.Length;
            var noise = ((float)rng.NextDouble() * 2f) - 1f;
            var cutoff = 0.02f + (0.25f * Mathf.Sin(Mathf.Pi * t));
            lp += (noise - lp) * cutoff;
            s[i] = lp * Mathf.Sin(Mathf.Pi * t);
        }

        Normalize(s, 0.35f);
        return ToWav(s);
    }

    /// <summary>Soft mallet: fundamental plus a quickly decaying 4th harmonic.</summary>
    private static AudioStreamWav Marimba(float freq, float length)
    {
        var s = Buffer(length);
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)Rate;
            var attack = Mathf.Min(1f, t * 400f);
            var fundamental = (float)Math.Sin(Math.Tau * freq * t) * Mathf.Exp(-t * 4.5f);
            var overtone = (float)Math.Sin(Math.Tau * freq * 4 * t) * Mathf.Exp(-t * 22f) * 0.25f;
            var octave = (float)Math.Sin(Math.Tau * freq * 2 * t) * Mathf.Exp(-t * 9f) * 0.2f;
            s[i] = (fundamental + overtone + octave) * attack;
        }

        Normalize(s, 0.6f);
        return ToWav(s);
    }

    /// <summary>Rolled bell-like arpeggio.</summary>
    private static AudioStreamWav Chime(float[] steps, float spacing, float length, float peak, int wave = 0)
    {
        var s = Buffer(length + (steps.Length * spacing));
        for (var n = 0; n < steps.Length; n++)
        {
            var freq = Freq(steps[n]);
            var start = (int)(n * spacing * Rate);
            for (var i = start; i < s.Length; i++)
            {
                var t = (i - start) / (float)Rate;
                var env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 3.2f);
                float v;
                if (wave == 0)
                {
                    v = (float)Math.Sin(Math.Tau * freq * t)
                        + ((float)Math.Sin(Math.Tau * freq * 2.76 * t) * 0.3f * Mathf.Exp(-t * 6f))
                        + ((float)Math.Sin(Math.Tau * freq * 5.4 * t) * 0.12f * Mathf.Exp(-t * 10f));
                }
                else
                {
                    v = (float)Math.Sin(Math.Tau * freq * 0.5 * t) + ((float)Math.Sin(Math.Tau * freq * t) * 0.2f);
                    env *= Mathf.Exp(-t * 2f);
                }

                s[i] += v * env;
            }
        }

        Normalize(s, peak);
        return ToWav(s);
    }

    private static AudioStreamWav Fanfare(float[] arpeggio, float[] chord)
    {
        var s = Buffer(2.6f);
        const float spacing = 0.09f;
        for (var n = 0; n < arpeggio.Length; n++)
        {
            var freq = Freq(arpeggio[n]);
            var start = (int)(n * spacing * Rate);
            for (var i = start; i < Math.Min(s.Length, start + (int)(0.5f * Rate)); i++)
            {
                var t = (i - start) / (float)Rate;
                s[i] += Triangle(freq * t) * Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 7f) * 0.6f;
            }
        }

        var chordStart = (int)(arpeggio.Length * spacing * Rate);
        foreach (var step in chord)
        {
            var freq = Freq(step);
            for (var i = chordStart; i < s.Length; i++)
            {
                var t = (i - chordStart) / (float)Rate;
                var env = Mathf.Min(1f, t * 60f) * Mathf.Exp(-t * 1.6f);
                s[i] += ((Triangle(freq * t) * 0.5f) + ((float)Math.Sin(Math.Tau * freq * t) * 0.5f)) * env * 0.35f;
            }
        }

        Normalize(s, 0.55f);
        return ToWav(s);
    }

    private static float Triangle(double cycles)
    {
        var x = cycles - Math.Floor(cycles);
        return (float)(4 * Math.Abs(x - 0.5) - 1);
    }

    /// <summary>Warm sustained chord with slow attack and gentle chorus.</summary>
    private static AudioStreamWav Pad(int[] semitones, float length)
    {
        var s = Buffer(length);
        foreach (var st in semitones)
        {
            var f = FreqC4(st) * 0.5f;
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)Rate;
                var env = Mathf.SmoothStep(0f, 2.2f, t) * (1f - Mathf.SmoothStep(length - 3.2f, length, t));
                var detune = 1f + (0.003f * Mathf.Sin(t * 0.7f));
                var v = (float)Math.Sin(Math.Tau * f * t)
                        + ((float)Math.Sin(Math.Tau * f * detune * 1.002 * t) * 0.6f)
                        + ((float)Math.Sin(Math.Tau * f * 2 * t) * 0.15f);
                s[i] += v * env;
            }
        }

        Normalize(s, 0.4f);
        return ToWav(s);
    }

    /// <summary>Seamless surf loop: brown noise shaped by slow overlapping swells.</summary>
    private static AudioStreamWav Ocean()
    {
        const float length = 16f;
        var s = Buffer(length);
        var rng = new Random(11);
        var brown = 0f;
        var lp = 0f;
        for (var i = 0; i < s.Length; i++)
        {
            var t = i / (float)Rate;
            var white = ((float)rng.NextDouble() * 2f) - 1f;
            brown = (brown + (white * 0.02f)) * 0.998f;
            lp += (white - lp) * 0.08f;
            var swellA = 0.5f + (0.5f * Mathf.Sin(Mathf.Tau * t / length * 2f));
            var swellB = 0.5f + (0.5f * Mathf.Sin((Mathf.Tau * t / length * 3f) + 1.3f));
            s[i] = (brown * 3.5f * (0.4f + (0.6f * swellA))) + (lp * 0.25f * swellB * swellB);
        }

        // Crossfade the tail into the head so the loop point is inaudible.
        var fade = Rate;
        for (var i = 0; i < fade; i++)
        {
            var k = i / (float)fade;
            s[i] = (s[i] * k) + (s[s.Length - fade + i] * (1f - k));
        }

        Array.Resize(ref s, s.Length - fade);
        Normalize(s, 0.7f);
        return ToWav(s, loop: true);
    }
}
