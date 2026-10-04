using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace MozaWheelControl;

/// <summary>
/// Точка входа: ожидание устройства, keyboard/socket mode, диспетчер команд,
/// гарантированное обнуление момента при любом выходе.
/// </summary>
internal static class Program
{
    private static readonly CancellationTokenSource Cts = new();
    private static readonly SemaphoreSlim DispatchGate = new(1, 1); // сериализация команд (консоль + сокет)

    private static Config _cfg = new();
    private static MozaWheel? _wheel;
    private static SocketServer? _socket;
    private static volatile bool _modeSocket; // false = keyboard mode (по умолчанию)
    private static int _shutdownDone;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Приоритет выше нормы: игра (загрузка/хичи) может вытеснять петлю 200 Гц —
        // тики проскакиваются, ошибка копится и после дыры сила хлещет через цель.
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal; }
        catch { /* не вышло — работаем на обычном приоритете */ }

        // --- Один инстанс: если программа уже запущена — закрываем старую, продолжаем здесь ---
        CloseOtherInstances();

        // --- Конфиг и режим ---
        _cfg = Config.Load();
        if (args.Any(a => a is "--simulate" or "-s"))
            _cfg.Simulate = true;

        // Ctrl+C / завершение процесса: сначала обнуляем момент, потом выходим.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = false; // дать процессу завершиться ПОСЛЕ обработчика
            Shutdown();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

        Console.WriteLine("Moza Wheel Control (Фаза 1)");
        Console.WriteLine($"Config: {Config.FilePath}");
        // Диагностика: SDK может падать по-разному от админа и без — фиксируем в логе.
        using (var id = WindowsIdentity.GetCurrent())
            Console.WriteLine($"Elevation: {(new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator) ? "admin" : "user (без админа)")}");

        // --- Подключение ---
        bool simulated = _cfg.Simulate;
        string deviceId = "SIMULATED";

        if (!simulated)
        {
            Console.WriteLine("Waiting for device...");

            if (!MozaWheel.InstallSdk(out string sdkError))
            {
                Console.WriteLine($"! Ошибка инициализации SDK: {sdkError}");
                if (!AskSimulation()) return 1;
                simulated = true;
            }
            else
            {
                string? found = await WaitForDeviceAsync();
                if (found is null)
                {
                    if (!AskSimulation()) return 1;
                    simulated = true;
                }
                else
                {
                    deviceId = found;
                }
            }
        }

        // --- Создать колесо и подключить ---
        var wheel = new MozaWheel(_cfg, simulated, deviceId);
        if (!wheel.Connect(out string connError))
        {
            Console.WriteLine($"! Не удалось подключиться: {connError}");
            wheel.Dispose();
            if (simulated || !AskSimulation())
            {
                Console.WriteLine("Выход.");
                return 1;
            }
            wheel = new MozaWheel(_cfg, true, "SIMULATED");
            wheel.Connect(out _);
        }
        _wheel = wheel;
        wheel.Start();

        PrintBanner(wheel);

        // --- Автозапуск UDP-сервера, если включён socketAutoStart в config.json ---
        if (_cfg.SocketAutoStart && _socket is not { IsRunning: true })
            await HandleLineAsync("socket", fromSocket: false);

        // --- Основной цикл консоли ---
        try
        {
            while (!Cts.IsCancellationRequested)
            {
                Console.Write(_modeSocket ? "socket> " : "keyboard> ");
                string? line = await Console.In.ReadLineAsync();
                if (line is null) break; // EOF (пайп/скрипт) — штатный выход

                if (!await HandleLineAsync(line, fromSocket: false))
                    break;
            }
        }
        finally
        {
            Shutdown();
        }

        return 0;
    }

    // ======================== Один инстанс ========================

    /// <summary>
    /// Закрыть другие запущенные экземпляры программы (нужно, чтобы два процесса
    /// не дрались за FFB-эффект). Ищем процессы с тем же именем, кроме текущего.
    /// CloseMainWindow НЕ используем: консоль у процессов может быть общей —
    /// закрыли бы чужое окно, поэтому сразу Kill.
    /// </summary>
    private static void CloseOtherInstances()
    {
        try
        {
            using var me = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(me.ProcessName))
            {
                try
                {
                    if (p.Id == me.Id) continue; // это мы сами
                    p.Kill();
                    p.WaitForExit(2000);
                    Console.WriteLine($"Закрыт предыдущий экземпляр [{p.ProcessName} #{p.Id}] — работает только этот.");
                }
                catch { /* уже завершился / нет прав — не мешаем старту */ }
                finally { p.Dispose(); }
            }
        }
        catch { /* диагностика не должна мешать старту */ }
    }

    // ======================== Ожидание устройства ========================

    /// <summary>
    /// Ждём руль до 10 секунд; дальше спрашиваем: симуляция или ждём дальше.
    /// </summary>
    /// <returns>Имя устройства или null (переход в симуляцию).</returns>
    private static async Task<string?> WaitForDeviceAsync()
    {
        bool warned = false;
        string? Poll()
        {
            string? id = MozaWheel.PollDevice(out string detail);
            // Необычный код ошибки (не «нет устройства») — покажем один раз, это важно для отладки на железе.
            if (id is null && !warned &&
                detail is not ("NODEVICES" or "NOINSTALLSDK" or "NORMAL" or "?"))
            {
                warned = true;
                Console.WriteLine($"! getMotorLimitAngle -> {detail} (продолжаю ждать; если руль подключён — проверь MOZA Pit House)");
            }
            return id;
        }

        long deadline = Environment.TickCount64 + 10_000;
        while (Environment.TickCount64 < deadline)
        {
            string? id = Poll();
            if (id is not null) return id;
            await Task.Delay(500);
        }

        while (true)
        {
            Console.WriteLine("! Устройство не найдено за 10 с.");
            Console.Write("  [Y] перейти в симуляцию (по умолчанию) / [w] продолжить ожидание руля: ");
            string? answer = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (answer is null or "" or "y" or "yes" or "д" or "да")
                return null; // симуляция

            if (answer is "w" or "wait" or "ж")
            {
                Console.WriteLine("Waiting for device... (Ctrl+C — выход)");
                while (true)
                {
                    string? id = Poll();
                    if (id is not null) return id;
                    await Task.Delay(500);
                }
            }
        }
    }

    private static bool AskSimulation()
    {
        while (true)
        {
            Console.Write("Перейти в режим симуляции? [Y/n]: ");
            string? answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (answer is null or "" or "y" or "yes" or "д" or "да") return true;
            if (answer is "n" or "no" or "н" or "нет")
            {
                Console.WriteLine("Без руля работать нечем — выход.");
                return false;
            }
        }
    }

    private static void PrintBanner(MozaWheel wheel)
    {
        string mode = wheel.IsSimulated ? "СИМУЛЯЦИЯ (роль не трогается)" : "ЖЕЛЕЗО";
        Console.WriteLine($"Connected device: [{wheel.GetDeviceId()}]  ({mode})");
        if (!wheel.IsSimulated && wheel.MotorLimits is { } limits)
            Console.WriteLine($"Motor limits: база {limits.Item1}° → физ ход ±{limits.Item1 / 2}° (консоль: эти градусы); игра/сокет: ±{wheel.SocketRangeHalf:0}° (userFullLockDeg)");
        Console.WriteLine($"Smooth: {_cfg.Smooth} ({_cfg.SmoothTimeMs} мс), holdForce: {_cfg.HoldForce}%, maxTorque: {_cfg.MaxTorque}%");

        float full = wheel.MoveRangeHalf; // физ. полный ход из Pit House (напр. ±90 при базе 180°)
        Console.WriteLine(
$"""
Команды:
  <угол>        повернуть к углу (0° = центр; шкала ±{full:0} = полный ход до упора), напр. 45
  +30 / -30     сдвиг относительно текущего положения (только в консоли)
  hold [угол]   удержание: hold {full:0} = полный ход до упора (без аргумента — текущая позиция)
  release       выключить удержание
  demo [сек] [ампл.°]  имитация езды: змейка на полный ход (50 с, ±{full:0});
                       сопротивление активно — можно взять руль в руки
  status        позиция/цель/режим/demo
  socket        запустить UDP-сервер ({_cfg.SocketHost}:{_cfg.SocketPort}) — Фаза 2
  keyboard      перейти обратно в keyboard mode
  sleep N       пауза N мс
  log [сек]     записать лог петли в osc_log.csv (диагностика раскачки)
  help          этот список
  exit          выход (момент обнуляется)
""");
    }

    // ======================== Диспетчер команд ========================

    /// <summary>Обработать одну строку. false = выход из программы.</summary>
    private static long _lastUnknownPrint; // троттлинг «Неизвестная команда» для пакетов сокета

    private static async Task<bool> HandleLineAsync(string line, bool fromSocket)
    {
        var cmd = InputHandler.Parse(line, fromSocket, invert: fromSocket && _cfg.InvertSocket);

        // Пауза НЕ держит общий семафор: иначе поток из сокета (30 Гц) встал бы за sleep.
        if (cmd.Kind == CommandKind.Sleep)
        {
            await Task.Delay((int)Math.Clamp(cmd.Value, 0, 60_000));
            return true;
        }

        await DispatchGate.WaitAsync();
        try
        {
            var wheel = _wheel;
            if (wheel is null) return false;

            switch (cmd.Kind)
            {
                case CommandKind.MoveAbsolute:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("отменена командой");
                    if (fromSocket && _cfg.SocketHandsMode)
                        wheel.HandsTo(cmd.Value, _cfg.HoldForce); // «держи меня за руки»: жёсткое слежение
                    else
                        wheel.SetPosition(cmd.Value, quiet: fromSocket);
                    if (wheel.IsHoldActive && !fromSocket)
                        Console.WriteLine("  (hold активен — руль удержан; release, чтобы освободить)");
                    break;
                }

                case CommandKind.MoveRelative:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("отменена командой");
                    float basePos = wheel.GetReferencePosition();
                    if (fromSocket && _cfg.SocketHandsMode)
                        wheel.HandsTo(basePos + cmd.Value, _cfg.HoldForce);
                    else
                        wheel.SetPosition(basePos + cmd.Value, quiet: fromSocket);
                    if (wheel.IsHoldActive && !fromSocket)
                        Console.WriteLine("  (hold активен — руль удержан; release, чтобы освободить)");
                    break;
                }

                case CommandKind.Hold:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("отменена командой");
                    float pos = cmd.Value; // NaN = без аргумента
                    if (float.IsNaN(pos))
                    {
                        pos = wheel.GetPosition(); // физические °
                        if (float.IsNaN(pos))
                            pos = _cfg.HoldPosition; // данных ещё нет — из конфига (физ. °)
                    }
                    if (fromSocket && !float.IsNaN(pos))
                        pos = wheel.SocketToPhysical(pos); // «hold» из игры — в её шкале
                    // Печатаем то, что реально станет целью (кламп ±физ. ходу руля).
                    pos = Math.Clamp(pos, -wheel.MoveRangeHalf, wheel.MoveRangeHalf);
                    wheel.EnableHold(pos, _cfg.HoldForce);
                    if (!wheel.IsSimulated)
                        Console.WriteLine($"Hold: удерживаю {pos:0.#}°, сила {_cfg.HoldForce}% (release — отпустить)" +
                                          (_socket is { IsRunning: true } ? "; сокет ждёт — игра не перехватит" : ""));
                    break;
                }

                case CommandKind.Release:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("отменена командой");
                    wheel.DisableHold();
                    Console.WriteLine("Hold выключен — руль свободен.");
                    break;
                }

                case CommandKind.Demo:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("перезапуск");
                    wheel.StartDemo(cmd.Value, cmd.Value2);
                    break;
                }

                case CommandKind.Log:
                {
                    wheel.StartLog(cmd.Value);
                    break;
                }

                case CommandKind.Status:
                {
                    float pos = wheel.GetPosition();
                    string posStr = float.IsNaN(pos) ? "нет данных" : $"{pos:0.#}°";
                    Console.WriteLine(
                        $"pos={posStr}  target={wheel.CurrentTarget:0.#}°  hold={(wheel.IsHoldActive ? $"ON ({_cfg.HoldForce}%)" : "OFF")}  " +
                        $"demo={(wheel.IsDemoActive ? "ON" : "OFF")}  " +
                        $"mode={(_modeSocket ? "socket" : "keyboard")}  device=[{wheel.GetDeviceId()}]  " +
                        $"sim={wheel.IsSimulated}  smooth={_cfg.Smooth}");

                    // Диагностика шкалы сокета: реально виденные числа из игры vs userFullLockDeg.
                    // Если max > шкалы — ввод обрезается, руль стоит у упора («дубнем»).
                    float sockMax = wheel.SocketMaxSeen;
                    if (sockMax > 0f)
                    {
                        string hint = sockMax > wheel.SocketRangeHalf + 0.5f
                            ? $" — ! за шкалой, ввод режется: подними userFullLockDeg до {sockMax:0.#}"
                            : "";
                        Console.WriteLine($"sock: max ±{sockMax:0.#} из игры (шкала ±{wheel.SocketRangeHalf:0.#}){hint}");
                    }
                    else
                    {
                        Console.WriteLine("sock: чисел из игры ещё не было (max — нет данных)");
                    }
                    break;
                }

                case CommandKind.Socket:
                {
                    if (_socket is { IsRunning: true })
                    {
                        _modeSocket = true;
                        wheel.AllowSocketControl();
                        Console.WriteLine($"Socket mode: сервер уже работает на {_socket.Endpoint}");
                        break;
                    }

                    _socket = new SocketServer(_cfg.SocketHost, _cfg.SocketPort,
                        async s => await HandleLineAsync(s, fromSocket: true));
                    string? error = _socket.Start();
                    if (error is not null)
                    {
                        Console.WriteLine($"! Не удалось запустить UDP-сервер: {error}");
                        _socket = null;
                    }
                    else
                    {
                        _modeSocket = true;
                        wheel.AllowSocketControl(); // новая сессия сокета — снимаем консольный перехват
                        Console.WriteLine($"Socket mode: слушаю UDP {_cfg.SocketHost}:{_cfg.SocketPort} " +
                                          $"(интервал данных ~{_cfg.DataIntervalMs} мс / ~{1000f / _cfg.DataIntervalMs:0} Гц). " +
                                          "Каждая датаграмма = одна команда — те же строки, что и в консоли.");
                        Console.WriteLine("  Lua (мод руля): cfg.udpSocket:send(tostring(round(angle))) — целое число подойдёт как есть.");
                        Console.WriteLine($"  Числа — углы ИГРЫ в ЕЁ шкале (±{wheel.SocketRangeHalf:0}): " +
                                          $"{wheel.SocketRangeHalf:0} = полный руль, 0 = центр, «-5» = левее центра.");
                        if (_cfg.InvertSocket)
                            Console.WriteLine("  Знак из игры инвертируется (invertSocket=true) — направление совпадает с рулём.");
                        if (_cfg.SocketHandsMode)
                            Console.WriteLine($"  Hands ON («держи меня за руки»): позиции держатся жёстко, силой {_cfg.HoldForce}% — " +
                                              "оттолкнуть руль от цели сложно. Выключить: socketHandsMode=false.");
                    }
                    break;
                }

                case CommandKind.Keyboard:
                {
                    _modeSocket = false;
                    Console.WriteLine("Keyboard mode (сервер сокета продолжает работать в фоне).");
                    break;
                }

                case CommandKind.Sleep:
                {
                    // Обрабатывается до захвата семафора (см. выше) — сюда не попадаем.
                    break;
                }

                case CommandKind.Help:
                    PrintBanner(_wheel!);
                    break;

                case CommandKind.Exit:
                    Cts.Cancel();
                    return false;

                default:
                    if (line.Trim().Length > 0)
                    {
                        if (fromSocket)
                        {
                            // Мусор из сокета не должен заливать консоль 30 строк/с.
                            long now = Environment.TickCount64;
                            if (now - _lastUnknownPrint >= 2000)
                            {
                                _lastUnknownPrint = now;
                                Console.WriteLine($"! Неизвестная команда из сокета: {line.Trim()} (help — список)");
                            }
                        }
                        else
                            Console.WriteLine($"! Неизвестная команда: {line.Trim()} (help — список)");
                    }
                    break;
            }

            return true;
        }
        finally
        {
            DispatchGate.Release();
        }
    }

    // ======================== Безопасный выход ========================

    /// <summary>Идемпотентно: обнулить момент и закрыть всё. Вызывается при exit, Ctrl+C, EOF, ошибке.</summary>
    private static void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownDone, 1) != 0) return;
        try { Cts.Cancel(); } catch { }

        try { _wheel?.Dispose(); } catch { /* лучший момент обнулить — выше всего */ }
        try { _socket?.DisposeAsync().AsTask().Wait(1000); } catch { }

        Console.WriteLine("Момент обнулён. Выход.");
    }
}
