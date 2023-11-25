namespace WalleeCharging.Meter;

public class MeterDataException : Exception
{
    public MeterDataException(string message) : base(message)
    {
    }

    public MeterDataException(string message, Exception inner) : base(message, inner)
    {
    }
}
