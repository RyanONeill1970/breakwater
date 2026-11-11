namespace Breakwater;

/// <summary>
/// A self-contained, animated art target. It never creates a campaign or reads/writes player saves.
/// Open ArtStudy.tscn with F6, or launch Godot with that scene as its positional argument.
/// </summary>
public partial class ArtStudy : Node3D
{
	private const float DefaultYaw = -.3f, DefaultPitch = .78f, DefaultDistance = 205;
	private const float OrbitSensitivity = .006f, ZoomRatio = 1.12f;
	private const int WarmupFrames = 90;
	private const string ScreenshotPrefix = "--screenshot=";
	private const string CaptureFramePrefix = "--screenshot-frame=";
	private const string ViewPrefix = "--art-view=";
	private readonly Camera3D camera = new() { Current = true, Far = 1500, Fov = 42 };
	private readonly List<double> frameTimes = [];
	private float yaw = DefaultYaw, pitch = DefaultPitch, distance = DefaultDistance, clock;
	private Vector3 focus = new(0, 1, 0);
	private bool dragging, paused, capturing, orbitPreview;
	private int frames, captureFrame = 150;
	private string? screenshotPath;
	// Created in _Ready before input and frame callbacks.
	private CanvasLayer captions = null!;
	private Label statistics = null!;

	public override void _Ready()
	{
		var args = OS.GetCmdlineUserArgs();
		screenshotPath = args.FirstOrDefault(a => a.StartsWith(ScreenshotPrefix, StringComparison.Ordinal))?[ScreenshotPrefix.Length..];
		if (int.TryParse(args.FirstOrDefault(a => a.StartsWith(CaptureFramePrefix, StringComparison.Ordinal))?[CaptureFramePrefix.Length..], out int requested))
		{
			captureFrame = Math.Max(WarmupFrames + 1, requested);
		}

		orbitPreview = args.Contains("--art-orbit");
		BuildLight();
		BuildLandscape();
		BuildScenery();
		BuildVehicles();
		AddChild(camera);
		BuildCaptions();
		SetView(args.FirstOrDefault(a => a.StartsWith(ViewPrefix, StringComparison.Ordinal))?[ViewPrefix.Length..] ?? "detail");
		UpdateCamera();
		GD.Print("ART STUDY READY: island, scanned rocks, palms, harbour and animated vehicles loaded; campaign untouched.");
	}

	public override void _Process(double delta)
	{
		frames++;
		if (frames > WarmupFrames)
		{
			frameTimes.Add(delta * 1000);
		}

		if (!paused)
		{
			clock += (float)delta;
			AnimateVehicles();
			if (orbitPreview)
			{
				yaw += (float)delta * .04f;
			}
		}

		UpdateCamera();
		foreach (var vehicle in artVehicles)
		{
			vehicle.Dust.SpeedScale = paused ? 0 : 1;
		}
		statistics.Text = $"{Engine.GetFramesPerSecond()} FPS   ·   {(paused ? "PAUSED" : "LIVE SCENE")}";
		if (screenshotPath != null && frames >= captureFrame && !capturing)
		{
			capturing = true;
			captions.Visible = false;
			RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, Callable.From(Capture), (uint)ConnectFlags.OneShot);
		}
	}

	public override void _UnhandledInput(InputEvent evt)
	{
		switch (evt)
		{
			case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left:
				dragging = mouse.Pressed;
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
				distance = Math.Max(45, distance / ZoomRatio);
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
				distance = Math.Min(360, distance * ZoomRatio);
				break;
			case InputEventMouseMotion motion when dragging:
				yaw -= motion.Relative.X * OrbitSensitivity;
				pitch = Math.Clamp(pitch + motion.Relative.Y * OrbitSensitivity, .18f, 1.35f);
				break;
			case InputEventKey { Pressed: true, Echo: false } key:
				switch (key.PhysicalKeycode)
				{
					case Key.Key1:
						SetView("overview");
						break;
					case Key.Key2:
						SetView("harbour");
						break;
					case Key.Key3:
						SetView("road");
						break;
					case Key.Key4:
						SetView("detail");
						break;
					case Key.Key5:
						SetView("quay");
						break;
					case Key.Key6:
						SetView("coast");
						break;
					case Key.Space:
						paused = !paused;
						break;
					case Key.H:
						captions.Visible = !captions.Visible;
						break;
					case Key.Escape:
						GetTree().Quit();
						break;
				}
				break;
		}
	}

	private void SetView(string view)
	{
		(focus, yaw, pitch, distance) = view switch
		{
			"harbour" => (new Vector3(-53, 2, 5), -.5f, .48f, 110f),
			"road" => (new Vector3(-4, 3, 43), -.12f, .38f, 56f),
			"detail" => (new Vector3(-54, 4, 8), -.8f, .62f, 113f),
			"quay" => (new Vector3(-81, 3, 10), -.65f, .5f, 66f),
			"coast" => (new Vector3(-44, 2, 54), -.35f, .38f, 65f),
			_ => (new Vector3(0, 1, 15), DefaultYaw, DefaultPitch, DefaultDistance),
		};
	}

	private void UpdateCamera()
	{
		camera.Position = focus + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * distance;
		camera.LookAt(focus);
	}

	private void Capture()
	{
		var result = GetViewport().GetTexture().GetImage().SavePng(screenshotPath);
		var ordered = frameTimes.Order().ToArray();
		double median = ordered.Length > 0 ? ordered[ordered.Length / 2] : 0;
		double p95 = ordered.Length > 0 ? ordered[(int)((ordered.Length - 1) * .95)] : 0;
		GD.Print($"ART STUDY CAPTURE: {result} {screenshotPath}");
		GD.Print($"ART STUDY FRAME TIMES: {ordered.Length} samples, median {median:0.00} ms, p95 {p95:0.00} ms; end-to-end process intervals, includes VSync; not GPU timings.");
		GD.Print($"ART STUDY DRAW: {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} calls, {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)} primitives, render {GetViewport().GetTexture().GetWidth()} x {GetViewport().GetTexture().GetHeight()}.");
		GetTree().Quit(result == Error.Ok ? 0 : 1);
	}

	private void BuildLight()
	{
		AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Sky,
				Sky = new Sky { SkyMaterial = new PanoramaSkyMaterial { Panorama = GD.Load<Texture2D>("res://assets/sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr") } },
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = Palette.Art.SkyFill,
				AmbientLightEnergy = .75f,
				ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
				TonemapMode = Godot.Environment.ToneMapper.Aces,
				TonemapExposure = 1.0f,
				SsaoEnabled = false,
				SsaoIntensity = 1.5f,
				SsaoRadius = 2.2f,
				GlowEnabled = false,
				FogEnabled = true,
				FogMode = Godot.Environment.FogModeEnum.Depth,
				FogDepthBegin = 360,
				FogDepthEnd = 950,
				FogLightColor = Palette.Art.Haze,
				FogSkyAffect = .25f,
			},
		});
		AddChild(new DirectionalLight3D
		{
			RotationDegrees = new(-29, -42, 0),
			LightColor = Palette.Art.Sun,
			LightEnergy = 1.35f,
			ShadowEnabled = true,
			DirectionalShadowMaxDistance = 390,
			ShadowBias = .025f,
			ShadowNormalBias = .7f,
			DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
		});
	}

	private void BuildCaptions()
	{
		captions = new CanvasLayer();
		AddChild(captions);
		var margin = new MarginContainer { OffsetLeft = 28, OffsetTop = 24, MouseFilter = Control.MouseFilterEnum.Ignore };
		captions.AddChild(margin);
		var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.AddChild(stack);
		var title = new Label { Text = "BREAKWATER / HARBOUR", MouseFilter = Control.MouseFilterEnum.Ignore };
		title.AddThemeColorOverride("font_color", Palette.Art.Ink);
		title.AddThemeColorOverride("font_shadow_color", Palette.Art.Panel);
		title.AddThemeConstantOverride("shadow_offset_x", 1);
		title.AddThemeConstantOverride("shadow_offset_y", 2);
		title.AddThemeFontSizeOverride("font_size", 24);
		stack.AddChild(title);
		var description = new Label { Text = "ART STUDY   ·   Drag to orbit / wheel to zoom\n1 Island   2 Harbour   3 Road   4 Detail   5 Quay   6 Coast\nSpace Pause vehicles   H Hide   Esc Exit" };
		description.AddThemeColorOverride("font_color", Palette.Art.Caption);
		description.AddThemeColorOverride("font_shadow_color", Palette.Art.Panel);
		description.AddThemeConstantOverride("shadow_offset_y", 2);
		stack.AddChild(description);
		statistics = new Label();
		statistics.AddThemeColorOverride("font_color", Palette.Art.Caption);
		stack.AddChild(statistics);
	}
}
