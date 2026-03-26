namespace CoffeeShopApi.Services;

public class KeepAliveStateStore
{
    private readonly object _sync = new();
    private DateTime _lastHeartbeatUtc = DateTime.MinValue;
    private string _lastSource = "unknown";

    public void RecordHeartbeat(string source)
    {
        lock (_sync)
        {
            _lastHeartbeatUtc = DateTime.UtcNow;
            _lastSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();
        }
    }

    public (DateTime lastHeartbeatUtc, string source) Snapshot()
    {
        lock (_sync)
        {
            return (_lastHeartbeatUtc, _lastSource);
        }
    }
}
