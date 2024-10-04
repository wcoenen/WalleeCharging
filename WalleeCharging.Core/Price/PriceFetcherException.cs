namespace WalleeCharging;

public class PriceFetcherException : Exception
{
    public PriceFetcherException(string message) : base(message)
    {
    }
    public PriceFetcherException(string message, Exception inner) : base(message, inner)
    {
    }
}
