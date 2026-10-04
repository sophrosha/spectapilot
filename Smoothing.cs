namespace MozaWheelControl;

/// <summary>
/// Математика сглаживания движения руля.
///
/// Задача: между входными пакетами (клавиатура — редко, сокет — ~30 Гц, т.е. каждые 33 мс)
/// руль не должен дёргаться. Решение: вход пишет только ЦЕЛЬ, а этот класс на каждом тике
/// цикла управления (200 Гц) плавно доводит текущую позицию к цели.
/// </summary>
public sealed class Smoothing
{
    private readonly Config _cfg;
    private float _current;
    private float _target;
    private float? _tauMsOverride; // переопределение времени (сокет: быстрая реакция вместо 500 мс)

    /// <summary>Текущая (сглаженная) позиция, градусы.</summary>
    public float Current => _current;

    /// <summary>Целевая позиция, градусы.</summary>
    public float Target => _target;

    /// <summary>Эффективное время сглаживания, мс (переопределение или config.smoothTimeMs).</summary>
    public float EffectiveTauMs => _tauMsOverride ?? _cfg.SmoothTimeMs;

    /// <summary>Переопределить время сглаживания, мс (null — вернуться к config.smoothTimeMs).</summary>
    public void SetTauMs(float? ms) => _tauMsOverride = ms;

    public Smoothing(Config cfg, float start = 0f)
    {
        _cfg = cfg;
        _current = _target = start;
    }

    /// <summary>Задать новую целевую позицию (не двигает руль мгновенно — только меняет цель).</summary>
    public void SetTarget(float target) => _target = target;

    /// <summary>Мгновенно перепрыгнуть (используется при инициализации).</summary>
    public void SnapTo(float value) => _current = _target = value;

    /// <summary>
    /// Один шаг сглаживания.
    /// </summary>
    /// <param name="dt">Фактическое время с прошлого тика, секунды.</param>
    /// <returns>Новая сглаженная позиция (градусы).</returns>
    public float Step(float dt)
    {
        if (dt <= 0f) return _current;

        // smooth: false — цель подхватывается сразу.
        // Скорость и момент при этом сдерживаются петлёй движения (MaxMoveSpeedRpm / MoveForceLimit),
        // поэтому «резкий» режим не означает «опасный».
        if (!_cfg.Smooth)
        {
            _current = _target;
            return _current;
        }

        // Экспоненциальное приближение: ~95% пути за EffectiveTauMs
        // (эквивалентно критически затухшему приближению, без перелёта через цель).
        float tau = Math.Max(EffectiveTauMs, 1) / 1000f / 3f;
        float k = 1f - MathF.Exp(-dt / tau);
        float next = _current + (_target - _current) * k;

        // Страховочный лимит скорости — защита от рывков при любых настройках конфига.
        next = MoveToward(_current, next, _cfg.MaxDegPerSec * dt);

        _current = next;
        return _current;
    }

    /// <summary>Переместить value к to не быстрее чем на maxDelta.</summary>
    public static float MoveToward(float from, float to, float maxDelta)
    {
        float delta = to - from;
        if (MathF.Abs(delta) <= maxDelta) return to;
        return from + MathF.Sign(delta) * maxDelta;
    }
}
