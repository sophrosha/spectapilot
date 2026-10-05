using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    /// <summary>Позиционная петля (порт official примера sdk_api_test.cc::moveTo).</summary>
    private float ComputeMoveForce(float pos, float vel, float dt)
    {
        float smoothed = _smoothing.Step(dt);
        float posError = smoothed - pos;

        // Только предупреждение — силу НЕ обнуляем: цель должна достигаться всегда.
        // (Раньше при >60° сила сбрасывалась навсегда: +100 и «0» не доезжали.)
        if (MathF.Abs(posError) > _rangeHalf * PosWarnFactor)
            Warn($"очень большая ошибка позиции {posError:0.#}° — если руль идёт не туда, выставь forceInvert в config.json.");

        // Позиция -> требуемая скорость (об/мин).
        // pos_kp подобран так, чтобы при тau=SmoothTimeMs получился 1.0 — как в примере SDK.
        // Из сокета tau маленькая (socketSmoothTimeMs) → реакция почти мгновенная.
        float posKp = _cfg.Smooth
            ? 0.5f / Math.Max(_smoothing.EffectiveTauMs / 1000f, 0.01f)
            : FastMovePosKp;
        float speedLimit = _cfg.Smooth ? _cfg.MaxMoveSpeedRpm : Math.Min(_cfg.MaxMoveSpeedRpm * 2f, 300f);

        float spdRef = posKp * posError;
        spdRef = Math.Clamp(spdRef, -speedLimit, speedLimit);

        // Скорость -> момент.
        float currentRpm = vel * DegPerSecToRpm;
        float spdErr = spdRef - currentRpm;

        _posInteg += spdErr * dt * SpdKi;
        // Интегратор может выйти на ВЕСЬ предел момента. (Раньше делили на SpdKi:
        // получалось 1500/40 = 37.5 ≈ 0.4% — руль не преодолевал трение и +10 не доезжал.)
        float integLimit = Math.Max(_cfg.MoveForceLimit, 100f);
        _posInteg = Math.Clamp(_posInteg, -integLimit, integLimit); // без виндапа

        float torqueRef = _posInteg + spdErr * SpdKp;

        // Знак: в официальном примере target_ref = -torque_ref.
        float magnitude = -torqueRef;
        if (_cfg.ForceInvert) magnitude = -magnitude;

        float cap = MathF.Min(_cfg.MoveForceLimit, 10_000f);
        float result = Math.Clamp(magnitude, -cap, cap);

        // ---- Плавный подход к упору (анти-«подпрыгивание как мячик") ----
        // Цель на клампе у механического стопа: интеграл копился весь ход и вдавливал
        // руль в упор → отскок → повторный удар (качается). У упора: стравливаем
        // интеграл и притормаживаем подачу — руль мягко прижимается и стоит.
        float toStop = smoothed >= 0f ? _rangeHalf - pos : _rangeHalf + pos; // до упора в сторону цели
        bool approachingStop = toStop < LockApproachDeg && (posError >= 0f) == (smoothed >= 0f);
        if (approachingStop)
        {
            _posInteg *= 0.85f; // стравить виндап у упора (каждый тик)
            if (toStop > 0f)
                result *= 0.3f + 0.7f * Math.Clamp(toStop / LockApproachDeg, 0f, 1f);
        }

        return result;
    }

    /// <summary>Удержание: пружина-демпфер. Мягко, но уверенно сопротивляется, без рывков.</summary>
    private float ComputeHoldForce(float pos, float vel, float dt)
    {
        // Разгон силы при включении (и после каждого входа в hold) — чтобы не дёргало.
        if (_cfg.HoldRampMs <= 0)
            _holdRamp = 1f;
        else
            _holdRamp = Math.Min(1f, _holdRamp + dt * 1000f / _cfg.HoldRampMs);

        float baseCap = Math.Clamp(_holdForcePct, 0f, 100f) / 100f * 10_000f; // % → шкала DirectInput
        if (baseCap <= 0f) return 0f;

        float err = _holdPos - pos;                     // >0 — руль левее цели

        // Прогрессивное усиление («чувствует мою руку»): если пользователь усилил
        // отклонение от цели, сила ОСТОРОЖНО поднимается к 100% — smoothstep, без ступенек.
        float cap = baseCap;
        if (_cfg.ProgressiveHold && cap < 10_000f)
        {
            float x = Math.Clamp(MathF.Abs(err) / ProgressiveBoostDeg, 0f, 1f);
            x = x * x * (3f - 2f * x);
            cap += (10_000f - cap) * x;
        }

        // KpScale/KdScale из config.json (по умолчанию 1.0 = прежнее поведение):
        // kpScale 0.5 = пружина вдвое мягче (меньше борьбы с рукой и щелчков стик-липа),
        // kdScale — демпфер (осторожно: <1 возвращает волну ~15 Гц, см. README).
        // Губернатор от KpScale не зависит (кламп по cap), равновесная скорость
        // подлёта = cap/kd = 180/KdScale °/с.
        float kp = cap / HoldSpringFullDeg * _cfg.KpScale;             // пружина: полная сила при 8° ошибки
        float kd = cap / HoldDampFullDegPerSec * _cfg.KdScale;         // демпфер: полный при 180 °/с

        // Сила в «+ угол»: тянет к цели и гасит движение (ни туда, ни сюда — не дёргается).
        // Губернатор подлёта: пружина ограничивается cap ДО сложения с демпфером.
        // Иначе при большом ветре (заряжена у упора / скачок цели) сумма kp·err − kd·v
        // даёт равновесную скорость kp·err/kd = 22.5·err — на скачке цели −95→0 руль
        // улетал на 697 °/с с шагом силы 6300 (лог езды 19:54, событие 94.94) — «стол
        // дрожит, руль хочет быстро вернуться». С клампом: v_равн = cap/kd = 180 °/с при
        // ЛЮБОЙ ошибке; на типичной работе |err|<8° пружина ниже cap — формула не меняется,
        // сила удержания и сопротивление рывку (err>8, скорость от руки) те же.
        float spring = Math.Clamp(kp * err, -cap, cap);
        float forcePlus = spring - kd * vel;

        // У упора (цель на клампе, руль у механического стопа): мягкие ритмичные толчки
        // 6..12% силы ~1.2 Гц — «осторожные, не сильные» (в игре может быть 3 витка,
        // а ход у нас один — упор подсказывает предел без жёсткого втыкания).
        if (_cfg.LockNudge && _lockTarget)
        {
            float dist = _rangeHalf - MathF.Abs(pos);   // сколько осталось до упора
            if (dist <= NudgeBlendDeg)
            {
                float prox = 1f - MathF.Max(dist, 0f) / NudgeBlendDeg;      // 0..1 к упору
                float phase = Environment.TickCount64 * 0.001f * 2f * MathF.PI * NudgeHz;
                float pulse = 0.06f + 0.06f * (0.5f + 0.5f * MathF.Sin(phase));
                float nudgePlus = cap * pulse * _lockDir;
                forcePlus += (nudgePlus - forcePlus) * prox;   // плавный переход пружина↔толчок
            }
        }

        float magnitude = _cfg.ForceInvert ? forcePlus : -forcePlus;
        magnitude *= _holdRamp;
        // ВАЖНО: slew-лимитера (ограничение скорости изменения силы) здесь НЕТ — он в петле
        // обратной связи давал фазовый сдвиг и раскачку руля до упора («качался 120..160»).
        // Плавность дают: EMA цели (_holdPos), мёртвая зона скорости и holdRampMs.
        return Math.Clamp(magnitude, -cap, cap);
    }

    /// <summary>Отдать момент на колесо (с восстановлением эффекта после сбоя).</summary>
    private void ApplyMagnitude(float magnitude)
    {
        if (_cf is null) return;
        try
        {
            float c = Math.Clamp(magnitude, -10_000f, 10_000f);
            _lastApplied = c;
            _cf.setMagnitude((long)c);
            if (_effectBroken)
            {
                _effectBroken = false;
                Warn("FFB-эффект восстановлен.");
            }
        }
        catch (Exception ex)
        {
            // Эффект мог «умереть» (например, по таймауту) — пробуем перезапустить один раз.
            if (!_effectBroken)
            {
                _effectBroken = true;
                try { _cf.stop(); } catch { }
                try { _cf.start(); Warn($"FFB-эффект перезапущен после сбоя: {ex.Message}"); }
                catch (Exception ex2) { Warn($"FFB-эффект не запускается: {ex2.Message}"); }
            }
        }
    }

    private void AnnounceReach(float pos)
    {
        if (!_targetSet || _announcedReach) return;
        float target = _smoothing.Target;
        if (MathF.Abs(target - pos) < 1.0f)
        {
            _announcedReach = true;
            _targetSet = false;
            Console.WriteLine($"Достигнуто: {target:0.#}°");
        }
    }

    /// <summary>Предупреждение (не чаще раза в 5 секунд, чтобы не спамить).</summary>
    private void Warn(string message)
    {
        LastWarning = message;
        long now = Environment.TickCount64;
        if (now - _lastWarnTick < 5000) return;
        _lastWarnTick = now;
        Console.WriteLine($"! {message}");
    }
}
