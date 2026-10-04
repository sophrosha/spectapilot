using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MozaWheelControl;

/// <summary>
/// UDP-сервер для socket mode. TCP не используется — мод руля шлёт только UDP.
///
/// Формат отправки (Lua, мод): cfg.udpSocket:send(tostring(round(angle))).
/// Каждая датаграмма = одна команда — те же строки, что и в консоли:
/// "45" (абсолютный угол), "+10" / "-10" (относительный), "hold 90", "release",
/// "status", "demo", "exit". Целые числа парсятся как есть.
///
/// Ответ клиенту не нужен: обратная связь видна в консоли приложения.
///
/// Рассчитан на ~30 Гц (датаграмма каждые ~33 мс от мода):
///  - обработка мгновенна (только меняет цель у руля), поэтому очередь не растёт;
///  - «latest-wins» по построению: каждая новая датаграмма перезаписывает цель,
///    а петля управления 200 Гц сама сглаживает переход — задержка не накапливается.
/// </summary>
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

    /// <param name="host">Адрес (по умолчанию localhost — сервер не торчит наружу).</param>
    /// <param name="port">UDP-порт (тот же, что в config: socketPort).</param>
    /// <param name="onLine">Callback на каждую команду (строку из датаграммы).</param>
    public SocketServer(string host, int port, Func<string, Task> onLine)
    {
        _host = host;
        _port = port;
        _onLine = onLine;
    }

    /// <summary>
    /// Запустить UDP-слушатель.
    /// </summary>
    /// <returns>null — успех; иначе текст ошибки (например, порт занят).</returns>
    public string? Start()
    {
        if (_udp is not null) return null; // уже работает
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

    /// <summary>Приём датаграмм: одна датаграмма = одна команда (многострочную режем по \n).</summary>
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
            catch (OperationCanceledException) { break; } // остановка сервера
            catch (SocketException) { break; }             // сокет закрыт
            catch (ObjectDisposedException) { break; }
            catch { /* битая датаграмма не должна ронять слушателя */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts?.Cancel();
            try { _udp?.Close(); } catch { /* и так остановится */ }
            if (_loop is not null)
            {
                try { await _loop.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch { /* и так останавливаемся */ }
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
