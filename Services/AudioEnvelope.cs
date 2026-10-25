namespace Yuta.Services;

// A short history of measured amplitude, not a frequency spectrum or a loop.
internal sealed class AudioEnvelope
{
    internal const int BarCount = 21;
    private readonly float[] _history = new float[BarCount];
    private float _level, _reference, _deviation;
    private bool _initialized;
    public static float Decibels(float peak) => !float.IsFinite(peak) || peak <= .001f ? -60 : Math.Clamp(20 * MathF.Log10(peak), -60, 0);
    public float[] Sample(float peak, double elapsedMilliseconds = 25)
    {
        peak = float.IsFinite(peak) ? Math.Clamp(peak, 0, 1) : 0;
        var elapsed = Math.Clamp(elapsedMilliseconds, 1, 1000);
        if (!_initialized && peak > .003f)
        {
            _reference = peak;
            _deviation = Math.Max(.035f, peak * .15f);
            _initialized = true;
        }
        // Keep sustained audio low and give its measured transients headroom.
        // Soft compression avoids the hard ceiling that made loud tracks look
        // stuck. The minimum deviation prevents amplification of tiny changes.
        var delta = peak - _reference;
        var transient = Math.Max(0, delta / Math.Max(.035f, _deviation * 2));
        var target = peak <= .003f ? 0 : .14f * MathF.Sqrt(peak) + .6f * transient / (1 + transient);
        // Suppress near-noise-floor input instead of normalizing it into movement.
        if (peak < .02f) target *= Math.Clamp((peak - .003f) / .017f, 0, 1);
        _reference += delta * (float)(1 - Math.Exp(-elapsed / 250));
        _deviation += (Math.Abs(delta) - _deviation) * (float)(1 - Math.Exp(-elapsed / 600));
        var response = 1 - Math.Exp(-elapsed / (target > _level ? 12 : 55));
        _level += (target - _level) * (float)response;
        if (_level < .006f) _level = 0;
        for (var i = _history.Length - 1; i > 0; i--) _history[i] = _history[i - 1];
        _history[0] = _level;
        // New measurements appear in the middle and travel outwards. Each side
        // shows different samples; this is not a mirrored or looping waveform.
        Span<float> spread = stackalloc float[BarCount];
        const int center = BarCount / 2;
        for (var i = 0; i < BarCount; i++)
            spread[i] = _history[i == center ? 0 : i < center ? (center - i) * 2 - 1 : (i - center) * 2];
        var bars = new float[BarCount];
        for (var i = 0; i < BarCount; i++)
        {
            var rounded = .7f * spread[i] + .15f * spread[Math.Max(0, i - 1)] + .15f * spread[Math.Min(BarCount - 1, i + 1)];
            var edge = Math.Min(1, Math.Min(i, BarCount - 1 - i) / 3f);
            bars[i] = rounded * (.24f + .76f * edge * edge * (3 - 2 * edge));
        }
        return bars;
    }

}
