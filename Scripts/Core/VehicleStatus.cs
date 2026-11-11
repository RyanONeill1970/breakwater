namespace Breakwater.Core;

public enum VehicleStatus
{
    Ready,
    Moving,
    Holding,
    Yielding,
    DirectControl,
    NoRoute,
}

public static class VehicleStatusText
{
    public static string Describe(this VehicleStatus status) => status switch
    {
        VehicleStatus.DirectControl => "Direct control",
        VehicleStatus.NoRoute => "No route to order",
        _ => status.ToString(),
    };
}
