# Классы

Основной код консоли — `src/WheelControl/`, настроек — `src/WheelControlSettings/`.

## Wheel (`src/WheelControl/Wheel/`)

Обёртка над Moza SDK и фоновый цикл управления. Partial-класс разбит по ролям: `Wheel.cs` (установка SDK, эффект ConstantForce, шкалы градусов), `Wheel.Connect` (подключение к базе), `Wheel.Loop` (тик 200 Гц), `Wheel.Api` (позиция/момент), `Wheel.Hold` (пружина-демпфер удержания), `Wheel.Force` (расчёт усилия, kpScale/kdScale), `Wheel.Socket` (пакеты из игры, hands-режим), `Wheel.Demo` (имитация езды), `Wheel.Log` (CSV-лог).

Ключевые методы: `InstallSdk()`, `Connect()`, `Start()` (запуск цикла), `SetPosition()`, `SetTorque()`, `GetPosition()`, `EnableHold()` / `DisableHold()`, `HandsTo()`, `StartDemo()` / `StopDemo()`, `StartLog()` / `StopLog()`, `SocketToPhysical()`, `Dispose()`.

## Program (`src/WheelControl/Program/`)

Точка входа и жизненный цикл консоли: `Main.cs` (главный поток, чтение команд из stdin и из сокета — `HandleSocketLineAsync`), `Init.cs` (пут DLL, глушение шума SDK, `StartSocketAsync`, `CloseOtherInstances`), `Log.cs` (лог-файл, режим `debug`), `Args.cs` (`--simulate` и прочие флаги), `DeviceWatch` / `FocusWait` / `DemoExit` / `Muted` (подсказки про базу, ожидание фокуса, завершение демо, приглушение вывода).

## Config (`src/WheelControl/Config/Config.cs`)

Конфигурация: свойства с дефолтами прямо в коде, чтение/запись `Assets/config.json` и защита диапазонов. Ключевые члены: `Load()`, `Save()`, `Normalize()` (прижимает значения к допустимым диапазонам), `FilePath`.

## SocketServer (`src/WheelControl/SocketServer/SocketServer.cs`)

UDP-слушатель режима сокета (TCP не используется — мод шлёт только UDP). Одна датаграмма = одна команда, разбор через общий `InputHandler`; ответа клиенту нет. Ключевые члены: `Start()`, `IsRunning`, `Endpoint`, `DisposeAsync()`; цикл приёма приватный (`ReceiveLoopAsync`).

## InputHandler (`src/WheelControl/InputHandler/InputHandler.cs`)

Парсер команд — один для консоли и сокета (разница в знаках: из сокета `-45` — позиция, из консоли — сдвиг). Ключевые члены: `Parse(line, fromSocket, invert)`, `TryParseNumber()`, enum `CommandKind`, структура `Command`.

## Smoothing (`src/WheelControl/Smoothing/Smoothing.cs`)

Математика плавного движения: вход только пишет цель, а `Step(dt)` на каждом тике доводит текущее положение до цели (по `smoothTimeMs`, либо по переопределению для сокета). Ключевые члены: `SetTarget()`, `Step()`, `SnapTo()`, `SetTauMs()`, свойства `Current` / `Target` / `EffectiveTauMs`.

## WheelControlSettings (кратко)

- `MainForm` (+ частичные файлы `Texts` / `Status` / `Demo`) — окно настроек: события кнопок, статус с автоочисткой 5 с, запуск Demo.
- `Layout` — вся разметка окна строится кодом (без дизайнера).
- `ConfigBinding` — поля таблицы генерируются из ключей `config.json`, неизвестные ключи сохраняются.
- `Strings` — 4 языка интерфейса (ru/en/uk/by), хаб `Strings.T()`.
- `AboutForm`, `AppInit`, `PathResolver`, `ProcessWatch` — окно «О программе», инициализация, пути, следение за процессом консоли.

---
[← Назад к оглавлению](../../README.md) | [Следующий раздел →](05-faq.md)
