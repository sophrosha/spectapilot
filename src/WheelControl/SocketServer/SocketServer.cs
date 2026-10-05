using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WheelControl;

// UDP server for socket mode. TCP is not used — the wheel mod only sends UDP.
//
// Send format (Lua, mod): cfg.udpSocket:send(tostring(round(angle))).
// Each datagram = one command — the same lines as in the console:
// "45" (absolute angle), "+10" / "-10" (relative), "hold 90", "release",
// "status", "demo", "exit". Integers are parsed as-is.
//
// No reply to the client: the feedback is visible in the app console.
//
// Built for ~30 Hz (a datagram every ~33 ms from the mod):
// - processing is instant (it only changes the wheel target), so the queue never grows;
// - "latest-wins" by construction: every new datagram overwrites the target,
// and the 200 Hz control loop smooths the transition itself — no latency builds up.
public sealed class SocketServer : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly Func<string, Task> _onLine;

    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public bool IsRunning => _udp is not null;
    public string Endpoint => $"{_host}:{_port}";

    public SocketServer(string host, int port, Func<string, Task> onLine)
    {
        _host = host;
        _port = port;
        _onLine = onLine;
    }

    // Start the UDP listener.
    public string? Start()
    {
        if (_udp is not null) return null; // already running
        try
        {
            _cts = new CancellationTokenSource();
            _udp = new UdpClient(new IPEndPoint(IPAddress.Parse(_host), _port));
            _loop = ReceiveLoopAsync(_cts.Token);
            return null;
        }
        catch (Exception ex)
        {
            _udp = null;
            _cts?.Dispose();
            _cts = null;
            return ex.Message;
        }
    }

    // Datagram receive: one datagram = one command (multi-line is split on \n).
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var datagram = await _udp!.ReceiveAsync(ct);
                string text = Encoding.UTF8.GetString(datagram.Buffer);
                string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string l in lines)
                    await _onLine(l);
            }
            catch (OperationCanceledException) { break; } // server stop
            catch (SocketException) { break; }             // socket closed
            catch (ObjectDisposedException) { break; }
            catch { /* a broken datagram must not kill the listener */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts?.Cancel();
            try { _udp?.Close(); } catch { /* it stops anyway */ }
            if (_loop is not null)
            {
                try { await _loop.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch { /* we are stopping anyway */ }
            }
        }
        catch { /* best effort */ }
        finally
        {
            _udp = null;
            _cts?.Dispose();
            _cts = null;
        }
    }
}
