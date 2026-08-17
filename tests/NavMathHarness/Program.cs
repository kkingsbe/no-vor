using System;
using NOVor.Core;

internal static class Program
{
    private static int _failures;

    private static void Main()
    {
        Equal(359d, NavMath.NormalizeDegrees(-1d), "normalize negative");
        Equal(1d, NavMath.NormalizeDegrees(361d), "normalize overflow");
        True(NavMath.IsToStation(0d, 10d), "ten degrees is TO");
        True(!NavMath.IsToStation(0d, 190d), "reciprocal is FROM");
        Equal(1852d, NavMath.CrossTrackMeters(0d, 1852d, 0d), "aircraft right of north course");
        Equal(-1852d, NavMath.CrossTrackMeters(0d, -1852d, 0d), "aircraft left of north course");
        Equal(-1852d, NavMath.CrossTrackMeters(90d, 0d, 1852d), "aircraft left of east course");
        Equal(1852d, NavMath.AlongTrackToThresholdMeters(0d, 0d, -1852d),
            "aircraft one mile before northbound threshold");
        Equal(-1852d, NavMath.AlongTrackToThresholdMeters(0d, 0d, 1852d),
            "aircraft one mile past northbound threshold");
        Equal(-1d, NavMath.CrossTrackDeflection(1852d, 1852d), "right error commands left");
        Equal(0.5d, NavMath.CrossTrackDeflection(-926d, 1852d), "left error commands right");
        Equal(1d, NavMath.CrossTrackDeflection(-3704d, 1852d), "cross track clamps");
        Equal(90d, NavMath.SteeringErrorDegrees(0d, 90d), "steer right error");
        Equal(-90d, NavMath.SteeringErrorDegrees(0d, 270d), "steer left error");
        Equal(2d, NavMath.SteeringErrorDegrees(359d, 1d), "steer wraparound");
        Equal(110d, NavMath.DriftCorrectedHeadingDegrees(100d, 90d, 80d), "left drift correction");
        Equal(90d, NavMath.DriftCorrectedHeadingDegrees(100d, 90d, 100d), "right drift correction");
        Equal(30d, NavMath.EtaSeconds(1852d, 1852d / 30d), "eta from closure");
        True(double.IsNaN(NavMath.EtaSeconds(1852d, 0.5d)), "no useful closure");
        Equal(5.209d, CdiScale.AngularFullScaleNm(30d), "VOR full scale at thirty miles");
        Equal(3.473d, CdiScale.AngularFullScaleNm(20d), "VOR full scale at twenty miles");
        Equal(1.736d, CdiScale.AngularFullScaleNm(10d), "VOR full scale at ten miles");
        Equal(0.521d, CdiScale.AngularFullScaleNm(3d), "VOR full scale at three miles");
        Equal(45d, CdiScale.InterceptHeadingDegrees(90d, 5d, 45d), "intercept turns left when right of course");
        Equal(55d, CdiScale.InterceptHeadingDegrees(10d, -5d, 45d), "intercept turns right when left of course");
        Equal(347.5d, CdiScale.InterceptHeadingDegrees(10d, 0.5d, 45d), "intercept shallows inside one mile");
        Equal(110d, GuidanceMath.CommandHeadingDegrees(false, 0d, 100d, 0d, 45d, 90d, 80d),
            "direct command applies left-drift correction");
        Equal(55d, GuidanceMath.CommandHeadingDegrees(true, 90d, 0d, 5d, 45d, 90d, 80d),
            "manual command uses right-of-course intercept with drift correction");
        Equal(45d, GuidanceMath.CommandHeadingDegrees(true, 10d, 0d, -5d, 45d, 90d, 100d),
            "manual command uses left-of-course intercept with drift correction");
        Equal(357d, GuidanceMath.CommandHeadingDegrees(true, 359d, 0d, 0d, 45d, 359d, 1d),
            "manual command converges to selected course across wraparound");
        var planned = RunwayGuidance.Evaluate(new RunwayGuidanceInput
        {
            CourseDegrees = 180d,
            CrossTrackNm = 4d,
            AlongTrackToThresholdNm = 15d,
            GroundSpeedKnots = 300d,
            HeadingDegrees = 180d,
            GroundTrackDegrees = 180d,
            MaxInterceptDegrees = 45d
        });
        Equal(161.565d, planned.DesiredTrackDegrees, "runway capture aims for three mile gate");
        Same(RunwayGuidancePhase.Intercept, planned.Phase, "runway starts in intercept phase");
        var late = RunwayGuidance.Evaluate(new RunwayGuidanceInput
        {
            CourseDegrees = 180d,
            CrossTrackNm = 1d,
            AlongTrackToThresholdNm = 2.5d,
            GroundSpeedKnots = 300d,
            HeadingDegrees = 180d,
            GroundTrackDegrees = 180d,
            MaxInterceptDegrees = 45d
        });
        Equal(135d, late.DesiredTrackDegrees, "inside gate uses maximum recovery intercept");
        var capture = RunwayGuidance.Evaluate(new RunwayGuidanceInput
        {
            CourseDegrees = 180d,
            CrossTrackNm = 0.2d,
            AlongTrackToThresholdNm = 6d,
            GroundSpeedKnots = 300d,
            HeadingDegrees = 180d,
            GroundTrackDegrees = 180d,
            MaxInterceptDegrees = 45d
        });
        True(capture.DesiredTrackDegrees > 135d && capture.DesiredTrackDegrees < 180d,
            "speed-aware rollout shallows before centerline");
        Same(RunwayGuidancePhase.Capture, capture.Phase, "rollout enters capture phase");
        var established = RunwayGuidance.Evaluate(new RunwayGuidanceInput
        {
            CourseDegrees = 180d,
            CrossTrackNm = 0.03d,
            AlongTrackToThresholdNm = 3d,
            GroundSpeedKnots = 300d,
            HeadingDegrees = 180d,
            GroundTrackDegrees = 183d,
            MaxInterceptDegrees = 45d
        });
        Same(RunwayGuidancePhase.Established, established.Phase, "centered inbound is established");
        var held = RunwayGuidance.Evaluate(new RunwayGuidanceInput
        {
            CourseDegrees = 180d,
            CrossTrackNm = 0.08d,
            AlongTrackToThresholdNm = 2d,
            GroundSpeedKnots = 300d,
            HeadingDegrees = 180d,
            GroundTrackDegrees = 188d,
            MaxInterceptDegrees = 45d,
            WasEstablished = true
        });
        Same(RunwayGuidancePhase.Established, held.Phase, "established hysteresis prevents chatter");
        var wind = RunwayGuidance.Evaluate(new RunwayGuidanceInput
        {
            CourseDegrees = 180d,
            CrossTrackNm = 0d,
            AlongTrackToThresholdNm = 3d,
            GroundSpeedKnots = 300d,
            HeadingDegrees = 170d,
            GroundTrackDegrees = 180d,
            MaxInterceptDegrees = 45d
        });
        Equal(170d, wind.CommandHeadingDegrees, "command diamond retains wind correction");
        var passed = RunwayGuidance.Evaluate(new RunwayGuidanceInput
        {
            CourseDegrees = 180d,
            CrossTrackNm = 0.02d,
            AlongTrackToThresholdNm = -0.1d,
            GroundSpeedKnots = 300d,
            HeadingDegrees = 180d,
            GroundTrackDegrees = 180d,
            MaxInterceptDegrees = 45d
        });
        Same(RunwayGuidancePhase.Passed, passed.Phase, "threshold crossing does not reverse guidance");
        Equal(180d, passed.DesiredTrackDegrees, "passed runway holds inbound course");
        True(!CdiScale.Evaluate(90d, 3d, 30d, CdiScaleMode.Angular, true, 1d, 45d).OffScale,
            "three miles is on scale at thirty miles");
        True(CdiScale.Evaluate(90d, 6d, 30d, CdiScaleMode.Angular, true, 1d, 45d).OffScale,
            "six miles is off scale at thirty miles");
        Equal(-0.576d, CdiScale.Evaluate(90d, 3d, 30d, CdiScaleMode.Angular, true, 1d, 45d).Deflection,
            "VOR deflection is based on angular range");
        Equal(1d, CdiScale.Evaluate(90d, -2d, 10d, CdiScaleMode.Angular, true, 1d, 45d).Deflection,
            "left of course clamps the needle right");
        Equal(1d, CdiScale.Evaluate(90d, 3d, 30d, CdiScaleMode.Angular, true, 1d, 45d).Side,
            "positive cross track is right of course");
        Equal("9.7 NM", NavigationPresentation.FormatDistance(9.7f, NavigationDisplayUnits.Aviation),
            "aviation distance");
        Equal("18.0 km", NavigationPresentation.FormatDistance(9.7f, NavigationDisplayUnits.Metric),
            "metric distance");
        Equal("166 KT", NavigationPresentation.FormatSpeed(166f, NavigationDisplayUnits.Aviation),
            "aviation speed");
        Equal("307 km/h", NavigationPresentation.FormatSpeed(166f, NavigationDisplayUnits.Metric),
            "metric speed");
        Equal("TO", NavigationPresentation.ToFromLabel(true), "to label");
        Equal("FROM", NavigationPresentation.ToFromLabel(false), "from label");
        var intercept = CockpitPresentation.Build(new CockpitPresentationInput
        {
            AirportName = "Dustbowl Airbase",
            DistanceNm = 10f,
            Course = 157f,
            Bearing = 202f,
            CommandHeading = 202f,
            GroundSpeedKnots = 0f,
            FullScaleNm = 1f,
            Manual = true,
            ToStation = false,
            OffScale = true,
            HasEta = false,
            ScaleMode = CdiScaleMode.Angular,
            Units = NavigationDisplayUnits.Aviation
        });
        Equal("DUSTBOWL  ·  10.0 NM", intercept.TargetLine, "cockpit target line");
        Equal("CRS 157°  ·  FROM", intercept.ContextLine, "cockpit manual context");
        Equal("INTCP  ◆  202°", intercept.CommandLine, "cockpit intercept command");
        Equal("ETA --:--  ·  GS 0 KT", intercept.SupportLine, "cockpit unavailable eta");
        Equal("VOR  ·  ±1 NM", intercept.ScaleLine, "cockpit full-deflection scale");
        var direct = CockpitPresentation.Build(new CockpitPresentationInput
        {
            AirportName = "Maris International Airport",
            DistanceNm = 9.7f,
            Bearing = 90.6f,
            CommandHeading = 97.6f,
            GroundSpeedKnots = 166f,
            EtaSeconds = 90f,
            Manual = false,
            HasEta = true,
            Units = NavigationDisplayUnits.Metric
        });
        Equal("BRG 091°  ·  DIRECT", direct.ContextLine, "cockpit direct context");
        Equal("STEER  ◆  098°", direct.CommandLine, "cockpit direct command");
        Equal("ETA 01:30  ·  GS 307 km/h", direct.SupportLine, "cockpit direct support");
        True(!direct.ShowCdi, "direct mode hides cdi rail");
        True(!direct.CommandAttention, "direct command is not intercept attention");
        var runwayReadout = CockpitPresentation.Build(new CockpitPresentationInput
        {
            AirportName = "Sandrift Airbase",
            RunwayLabel = "RWY 18",
            DistanceNm = 3f,
            Course = 184f,
            CommandHeading = 176f,
            GroundSpeedKnots = 300f,
            Manual = true,
            HasRunway = true,
            RunwayPhase = RunwayGuidancePhase.Capture,
            ScaleMode = CdiScaleMode.Angular,
            Units = NavigationDisplayUnits.Aviation
        });
        Equal("SANDRIFT  RWY 18  ·  3.0 NM", runwayReadout.TargetLine,
            "cockpit confirms selected runway");
        Equal("CRS 184°  ·  RWY", runwayReadout.ContextLine, "runway course context");
        Equal("INTCP  ◆  176°", runwayReadout.CommandLine, "diamond is explicit fly-to command");
        True(runwayReadout.CommandAttention, "runway capture command receives attention");
        True(InputBindingPolicy.IsJoystickButtonName("JoystickButton5"), "generic joystick button recognized");
        True(InputBindingPolicy.IsJoystickButtonName("Joystick3Button12"), "device joystick button recognized");
        True(!InputBindingPolicy.IsJoystickButtonName("N"), "keyboard key is not joystick button");
        True(!InputBindingPolicy.IsJoystickButtonName("JoystickAxis1"), "joystick axis is not supported button");
        True(InputBindingPolicy.IsDeviceSpecificJoystickButtonName("Joystick3Button12"), "device joystick button preferred");
        True(!InputBindingPolicy.IsDeviceSpecificJoystickButtonName("JoystickButton5"), "generic joystick button fallback");
        Equal(2d, InputBindingPolicy.CapturePreference("Joystick3Button12"), "device capture preference");
        Equal(1d, InputBindingPolicy.CapturePreference("JoystickButton5"), "generic capture preference");
        Equal(0d, InputBindingPolicy.CapturePreference("N"), "keyboard capture preference");

        var hotasGuid = new Guid("01234567-89ab-cdef-0123-456789abcdef");
        var hotas = new HotasBinding
        {
            DeviceName = "Thrustmaster Warthog",
            DeviceGuid = hotasGuid,
            DeviceId = 3,
            ButtonIndex = 11
        };
        HotasBinding reparsed;
        True(HotasBinding.TryParse(HotasBinding.Serialize(hotas), out reparsed),
            "hotas binding round trips through serialization");
        Equal("Thrustmaster Warthog", reparsed.DeviceName, "hotas device name round trip");
        Equal(hotasGuid.ToString(), reparsed.DeviceGuid.ToString(), "hotas device guid round trip");
        Equal(3d, reparsed.DeviceId, "hotas device id round trip");
        Equal(11d, reparsed.ButtonIndex, "hotas button index round trip");
        Equal("Thrustmaster Warthog B12", HotasBinding.Format(hotas), "hotas label is name plus one-based button");
        True(!HotasBinding.TryParse("junk", out HotasBinding junk), "garbage hotas binding rejected");
        True(!HotasBinding.TryParse("", out HotasBinding empty), "empty hotas binding rejected");
        True(HotasBinding.Serialize(HotasBinding.Empty) == "", "empty hotas binding serializes empty");
        Equal("<NOT BOUND>", HotasBinding.Format(HotasBinding.Empty), "empty hotas binding formats unbound");

        var pipeName = new HotasBinding
        {
            DeviceName = "Throttle|Stick",
            DeviceGuid = hotasGuid,
            DeviceId = 1,
            ButtonIndex = 0
        };
        True(HotasBinding.TryParse(HotasBinding.Serialize(pipeName), out HotasBinding pipeBack),
            "hotas binding name with pipe parses");
        Equal("Throttle|Stick", pipeBack.DeviceName, "hotas name containing pipe survives round trip");

        if (_failures > 0) Environment.Exit(1);
        Console.WriteLine("NavMathHarness: 86 passed");
    }

    private static void Equal(double expected, double actual, string name)
    {
        if (Math.Abs(expected - actual) <= 0.001d) return;
        Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
        _failures++;
    }

    private static void Equal(string expected, string actual, string name)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal)) return;
        Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
        _failures++;
    }

    private static void True(bool value, string name)
    {
        if (value) return;
        Console.Error.WriteLine($"FAIL {name}");
        _failures++;
    }

    private static void Same(CdiScaleMode expected, CdiScaleMode actual, string name)
    {
        if (expected == actual) return;
        Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
        _failures++;
    }

    private static void Same(RunwayGuidancePhase expected, RunwayGuidancePhase actual, string name)
    {
        if (expected == actual) return;
        Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
        _failures++;
    }
}
