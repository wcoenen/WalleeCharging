namespace WalleeCharging;

public class ChargingStationException : Exception
{
    public ChargingStationException(string message, Exception? inner) : base(message, inner)
    {
    }
}
