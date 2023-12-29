namespace WalleeCharging.Control;

public class InconsistentDataException : Exception
{
    public InconsistentDataException(string message) : base(message) { }
}
