using DanaProcessing.Midi;
using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>MIDI input: the byte-stream parser, the shared hub, OnMidi/MidiCC, and MIDI learn in the [Tweak] panel.</summary>
public class MidiTests : IDisposable
{
    // Same panel geometry as TweakTests (400-wide sketch): the MIDI pill sits at
    // the right end of the header, x = 334..378, y = 15..31; row 0 is y = 36..72.
    private const float PillX = 356, PillY = 22;
    private const float TrackStart = 166, TrackEnd = 378;

    public MidiTests()
    {
        MidiHub.DevicesEnabled = false; // never open the CI machine's real MIDI stack
        MidiHub.ResetState();
        TweakPanel.ClearAllBindings();
    }

    public void Dispose()
    {
        MidiHub.ResetState();
        TweakPanel.ClearAllBindings();
    }

    private static MidiMessage Cc(int controller, int value, int channel = 1) =>
        new((byte)(0xB0 | (channel - 1)), (byte)controller, (byte)value, "test");

    private static MidiMessage NoteOn(int note, int velocity, int channel = 1) =>
        new((byte)(0x90 | (channel - 1)), (byte)note, (byte)velocity, "test");

    private static void Frame(Sketch s, SkiaSharp.SKSurface surface)
    {
        surface.Canvas.Save();
        s.RenderFrame();
        surface.Canvas.Restore();
    }

    private static void Click(Sketch s, float x, float y)
    {
        s.MouseX = x;
        s.MouseY = y;
        s.RaiseMousePressed(MouseButtonKind.Left);
        s.RaiseMouseReleased(MouseButtonKind.Left);
        s.RaiseMouseClicked(MouseButtonKind.Left);
    }

    // -----------------------------------------------------------------
    // Parser
    // -----------------------------------------------------------------

    [Fact]
    public void Parser_handles_running_status_realtime_bytes_and_sysex()
    {
        var parser = new MidiStreamParser("dev");
        var got = new List<MidiMessage>();
        parser.Feed(new byte[]
        {
            0x90, 60, 100,          // note on
            62, 90,                 // running status: another note on
            0xF8,                   // clock in the middle of nothing
            0xB0, 21, 0xF8, 64,     // CC with a clock byte inside it
            0xF0, 1, 2, 3, 0xF7,    // sysex: skipped
            0x90, 60, 0,            // note on velocity 0 = note off
            0xC1, 5,                // program change (1 data byte), channel 2
        }, got.Add);

        Assert.Equal(5, got.Count);
        Assert.Equal((MidiKind.NoteOn, 60, 100), (got[0].Kind, got[0].Note, got[0].Velocity));
        Assert.Equal((MidiKind.NoteOn, 62, 90), (got[1].Kind, got[1].Note, got[1].Velocity));
        Assert.Equal((MidiKind.ControlChange, 21, 64), (got[2].Kind, got[2].Controller, got[2].Value));
        Assert.Equal(MidiKind.NoteOff, got[3].Kind);
        Assert.Equal((MidiKind.ProgramChange, 5, 2), (got[4].Kind, got[4].Value, got[4].Channel));
        Assert.All(got, m => Assert.Equal("dev", m.Device));
    }

    [Fact]
    public void Pitch_bend_is_centred_on_zero()
    {
        Assert.Equal(0, new MidiMessage(0xE0, 0x00, 0x40).Bend);
        Assert.Equal(-8192, new MidiMessage(0xE0, 0, 0).Bend);
        Assert.Equal(8191, new MidiMessage(0xE0, 0x7F, 0x7F).Bend);
    }

    // -----------------------------------------------------------------
    // Hub + sketch API
    // -----------------------------------------------------------------

    public class Listener : Sketch
    {
        public List<MidiMessage> Got = new();
        public override void Setup() => Size(200, 100);
        public override void Draw() { }
        public override void OnMidi(MidiMessage m) => Got.Add(m);
    }

    [Fact]
    public void Every_sketch_gets_every_message_once_with_its_own_cursor()
    {
        var a = new Listener();
        var b = new Listener();
        var sa = Offscreen.Run(a, 1); // first frame: cursors start at "now"
        var sb = Offscreen.Run(b, 1);

        MidiHub.Push(Cc(1, 10));
        MidiHub.Push(NoteOn(60, 99));

        Frame(a, sa);
        Frame(a, sa); // nothing new the second time
        Assert.Equal(2, a.Got.Count);

        Frame(b, sb); // b reads at its own pace: a didn't consume anything
        Assert.Equal(2, b.Got.Count);
        Assert.Equal(MidiKind.NoteOn, b.Got[1].Kind);
    }

    [Fact]
    public void A_sketch_never_replays_messages_from_before_it_started()
    {
        MidiHub.Push(Cc(1, 10));
        var late = new Listener();
        var surface = Offscreen.Run(late, 1);
        Frame(late, surface);
        Assert.Empty(late.Got);
    }

    public class Reader : Sketch
    {
        public override void Draw() { }
    }

    [Fact]
    public void MidiCC_and_notes_keep_the_latest_state_per_channel_and_for_any_channel()
    {
        var s = new Reader();
        MidiHub.Push(Cc(21, 127, channel: 2));
        MidiHub.Push(NoteOn(36, 80, channel: 10));

        Assert.Equal(1f, s.MidiCC(21));                 // any channel
        Assert.Equal(1f, s.MidiCC(21, channel: 2));
        Assert.Equal(0f, s.MidiCC(21, channel: 1));
        Assert.True(s.MidiNoteDown(36));
        Assert.Equal(80, s.MidiNoteVelocity(36, channel: 10));

        MidiHub.Push(NoteOn(36, 0, channel: 10));       // velocity 0 = released
        Assert.False(s.MidiNoteDown(36));
    }

    // -----------------------------------------------------------------
    // [Tweak] + MIDI
    // -----------------------------------------------------------------

    public class Knobs : Sketch
    {
        [Tweak(0, 10)] public float Speed = 5;
        [Tweak] public bool Trails = true;
        [Tweak] public Color Bg = new Color(0, 0, 0);
        public int Presses;
        public override void Setup() => Size(400, 300);
        public override void Draw() => Background(Bg);
        public override void MousePressed() => Presses++;
    }

    [Fact]
    public void Learn_connects_the_last_touched_slider_to_the_next_knob()
    {
        var s = new Knobs();
        var surface = Offscreen.Run(s, 1);

        Click(s, PillX, PillY);                         // MIDI learn on
        Assert.Equal(0, s.Presses);                     // the panel kept the click

        Click(s, TrackStart + (TrackEnd - TrackStart) * 0.5f, 62); // touch the Speed slider
        MidiHub.Push(Cc(21, 127));                      // turn a knob
        Frame(s, surface);
        Assert.Equal(10f, s.Speed, 3);                  // learned and applied

        MidiHub.Push(Cc(21, 0));
        Frame(s, surface);
        Assert.Equal(0f, s.Speed, 3);

        MidiHub.Push(Cc(22, 127));                      // an unconnected knob changes nothing
        Frame(s, surface);
        Assert.Equal(0f, s.Speed, 3);

        Assert.Contains(s.GetTweakValues(), v => v.FieldName == "Speed" && v.Touched);
    }

    [Fact]
    public void Learning_is_off_until_the_pill_is_clicked()
    {
        var s = new Knobs();
        var surface = Offscreen.Run(s, 1);

        Click(s, TrackStart, 62);                       // touch Speed without learning
        MidiHub.Push(Cc(21, 127));
        Frame(s, surface);
        Assert.Equal(0f, s.Speed, 3);                   // still where the click put it
    }

    public class Mapped : Sketch
    {
        [Tweak(0, 100)] public float Radius = 50;
        [Tweak] public bool On;
        [Tweak] public Color Bg = new Color(0, 0, 0);
        public override void Setup()
        {
            Size(400, 300);
            MapMidi("Radius", 7);
            MapMidi("bg.g", 8, channel: 3);
            MapMidiNote("On", 36);
        }
        public override void Draw() { }
    }

    [Fact]
    public void MapMidi_from_code_drives_numbers_colors_and_bools()
    {
        var s = new Mapped();
        var surface = Offscreen.Run(s, 1);

        MidiHub.Push(Cc(7, 127));
        MidiHub.Push(Cc(8, 127, channel: 3));
        MidiHub.Push(Cc(8, 0, channel: 1));             // wrong channel: ignored
        MidiHub.Push(NoteOn(36, 100));
        Frame(s, surface);

        Assert.Equal(100f, s.Radius, 3);
        Assert.Equal(255, s.Bg.G);
        Assert.Equal(0, s.Bg.R);
        Assert.True(s.On);

        MidiHub.Push(NoteOn(36, 100));                  // a second hit toggles back
        Frame(s, surface);
        Assert.False(s.On);
    }

    [Fact]
    public void Connections_survive_a_new_run_of_the_same_sketch_class()
    {
        var first = new Knobs();
        var s1 = Offscreen.Run(first, 1);
        Click(first, PillX, PillY);
        Click(first, TrackStart, 62);
        MidiHub.Push(Cc(30, 64));
        Frame(first, s1);

        var second = new Knobs();                       // "Run" again: a brand-new instance
        var s2 = Offscreen.Run(second, 1);
        MidiHub.Push(Cc(30, 127));
        Frame(second, s2);
        Assert.Equal(10f, second.Speed, 3);
    }

    [Fact]
    public void One_knob_drives_one_slider()
    {
        var s = new Knobs();
        var surface = Offscreen.Run(s, 1);
        s.MapMidi("Speed", 40);
        s.MapMidi("bg.r", 40);                          // moves the knob from Speed to Bg.R

        MidiHub.Push(Cc(40, 127));
        Frame(s, surface);
        Assert.Equal(5f, s.Speed, 3);
        Assert.Equal(255, s.Bg.R);
    }
}
