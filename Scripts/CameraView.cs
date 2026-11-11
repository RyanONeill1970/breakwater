namespace Breakwater;

/// <summary>What the screen shows. The sight and the four windows are Classic-mode views.</summary>
public enum CameraView
{
    Tactical,
    Chase,

    /// <summary>First person along the selected leader's gun.</summary>
    Sight,

    /// <summary>Four windows, one per group, each showing a sight, a map or a status report.</summary>
    Windows,
}
