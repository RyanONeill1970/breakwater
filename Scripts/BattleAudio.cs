namespace Breakwater;

/// <summary>
/// Procedurally synthesised combat and radio cues. Combat sounds are positional; radio is not. With
/// retro sound on, every cue is resynthesised as crunchy 8-bit samples at a low rate and only four
/// play at once, two combat and two radio, like a four-channel early-90s sound chip.
/// </summary>
public partial class BattleAudio : Node
{
    private const int RadioVoiceCount = 8;
    private const int CombatVoiceCount = 12;

    // Base mix levels in dB, before the player's volume settings.
    private const float RadioBaseDb = -20;
    private const float CombatBaseDb = -13;

    // Positional falloff for combat sounds, in metres.
    private const float CombatUnitSize = 35;
    private const float CombatMaxDistance = 180;
    private const float CombatMaxDb = -8;
    private const float CombatPanning = .8f;

    // The listener floats above the tactical focus or chase vehicle.
    private const float ListenerHeight = 8;
    private const float SoundHeight = 1.5f;

    /// <summary>Gain used instead of zero when converting to dB, which would be −∞.</summary>
    private const float SilentGain = .0001f;

    // Synthesis.
    private const int SampleRate = 22050;

    // Retro sound: 8-bit samples at the classic 8,363 Hz tracker rate, on four channels in all.
    private const int RetroSampleRate = 8363;
    private const int RetroCombatVoices = 2, RetroRadioVoices = 2;
    private const float Amplitude = 20000;
    private const float DecayRate = 6;
    private const int AttackSamples = 50;
    private const float NoiseMix = .45f;
    private const int NoiseSeed = 31;

    private readonly AudioStreamPlayer[] voices = new AudioStreamPlayer[RadioVoiceCount];
    private readonly AudioStreamPlayer3D[] combatVoices = new AudioStreamPlayer3D[CombatVoiceCount];

    // Assigned in _Ready, which Godot calls before anything else touches this node.
    private AudioListener3D listener = null!;
    private AudioStreamWav shot = null!, impact = null!, radio = null!, alarm = null!, boom = null!, whistle = null!;

    private bool muted;
    private bool? retro;
    private int voice;
    private float combatGain = 1, radioGain = 1;

    public bool Muted
    {
        get => muted;
        set
        {
            muted = value;
            if (value)
            {
                StopAll();
            }
        }
    }

    public void Configure(GameSettings settings)
    {
        Muted = settings.Muted;
        combatGain = settings.MasterVolume * settings.CombatVolume;
        radioGain = settings.MasterVolume * settings.RadioVolume;
        if (retro != settings.RetroSound)
        {
            retro = settings.RetroSound;
            Synthesise(settings.RetroSound);
        }

        foreach (var player in combatVoices)
        {
            player.VolumeDb = CombatBaseDb + Mathf.LinearToDb(Math.Max(SilentGain, combatGain));
            if (combatGain == 0)
            {
                player.Stop();
            }
        }
        foreach (var player in voices)
        {
            player.VolumeDb = RadioBaseDb + Mathf.LinearToDb(Math.Max(SilentGain, radioGain));
            if (radioGain == 0)
            {
                player.Stop();
            }
        }
    }

    public override void _Ready()
    {
        Synthesise(false);
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = new AudioStreamPlayer { VolumeDb = RadioBaseDb };
            AddChild(voices[i]);
        }

        for (int i = 0; i < combatVoices.Length; i++)
        {
            combatVoices[i] = new AudioStreamPlayer3D
            {
                MaxDb = CombatMaxDb,
                MaxDistance = CombatMaxDistance,
                PanningStrength = CombatPanning,
                UnitSize = CombatUnitSize,
                VolumeDb = CombatBaseDb,
            };
            AddChild(combatVoices[i]);
        }

        listener = new AudioListener3D();
        AddChild(listener);
        listener.MakeCurrent();
    }

    public void SetListener(Vector3 position) => listener.Position = position + Vector3.Up * ListenerHeight;

    public void SetPaused(bool paused)
    {
        foreach (var player in combatVoices)
        {
            player.StreamPaused = paused;
        }
    }

    public void PlayEvent(CombatEvent evt)
    {
        if (Muted || !evt.PlayerVisible || DisplayServer.GetName() == "headless")
        {
            return;
        }

        switch (evt.Kind)
        {
            case CombatEventKind.Shot or CombatEventKind.Impact or CombatEventKind.Destroy or CombatEventKind.Barrage or CombatEventKind.MineBlast or CombatEventKind.BuildingDestroyed:
                if (combatGain == 0)
                {
                    return;
                }

                var source = combatVoices[voice++ % (retro == true ? RetroCombatVoices : combatVoices.Length)];
                source.Position = BattleView.World(evt.Position, SoundHeight);
                source.Stream = evt.Kind switch
                {
                    CombatEventKind.Shot => shot,
                    CombatEventKind.Barrage or CombatEventKind.MineBlast or CombatEventKind.BuildingDestroyed => boom,
                    _ => impact,
                };
                source.Play();
                break;

            case CombatEventKind.Order or CombatEventKind.Capture or CombatEventKind.Contact or
                CombatEventKind.Assault or CombatEventKind.Reinforcement or CombatEventKind.SiteLost or CombatEventKind.ArtilleryCalled or CombatEventKind.IncomingArtillery:
                if (radioGain == 0)
                {
                    return;
                }

                var player = voices[voice++ % (retro == true ? RetroRadioVoices : voices.Length)];
                player.Stream = evt.Kind switch
                {
                    CombatEventKind.IncomingArtillery => whistle,
                    CombatEventKind.Assault or CombatEventKind.Reinforcement or CombatEventKind.SiteLost => alarm,
                    _ => radio,
                };
                player.Play();
                break;
        }
    }

    private void StopAll()
    {
        foreach (var p in voices)
        {
            p?.Stop();
        }

        foreach (var p in combatVoices)
        {
            p?.Stop();
        }
    }

    /// <summary>Builds every cue, as smooth 16-bit samples or, when <paramref name="crunchy"/>, low-rate 8-bit ones.</summary>
    private void Synthesise(bool crunchy)
    {
        shot = Tone(.16f, 72, true, crunchy);
        impact = Tone(.22f, 44, true, crunchy);
        radio = Tone(.12f, 620, false, crunchy);
        // Lower and longer than the radio blip, so an enemy move stands out from routine traffic.
        alarm = Tone(.45f, 330, false, crunchy);
        // Classic support weapons: a deep, long blast for shells and mines, and a falling whistle for incoming fire.
        boom = Tone(.6f, 34, true, crunchy);
        whistle = Tone(.9f, 1400, false, crunchy, endFrequency: 380);
    }

    /// <summary>
    /// A decaying sine, optionally mixed with noise, as mono PCM: 16-bit, or 8-bit at a low rate when
    /// <paramref name="crunchy"/>. With <paramref name="endFrequency"/> the pitch slides there.
    /// </summary>
    private static AudioStreamWav Tone(float seconds, float frequency, bool noise, bool crunchy, float? endFrequency = null)
    {
        int rate = crunchy ? RetroSampleRate : SampleRate;
        int count = (int)(seconds * rate);
        var data = new byte[count * (crunchy ? sizeof(sbyte) : sizeof(short))];
        var random = new Random(NoiseSeed);
        float phase = 0;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float pitch = endFrequency is float end ? frequency + (end - frequency) * t / seconds : frequency;
            phase += MathF.Tau * pitch / rate;
            float envelope = MathF.Exp(-t / seconds * DecayRate) * Math.Min(1, i / (float)AttackSamples);
            float sample = MathF.Sin(phase) * (noise ? NoiseMix : 1) +
                (noise ? (float)(random.NextDouble() * 2 - 1) * NoiseMix : 0);
            if (crunchy)
            {
                data[i] = unchecked((byte)(sbyte)(sample * envelope * sbyte.MaxValue));
                continue;
            }

            short value = (short)(sample * envelope * Amplitude);
            data[i * 2] = (byte)(value & byte.MaxValue);
            data[i * 2 + 1] = (byte)((value >> 8) & byte.MaxValue);
        }

        return new AudioStreamWav
        {
            Data = data,
            Format = crunchy ? AudioStreamWav.FormatEnum.Format8Bits : AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
        };
    }
}
