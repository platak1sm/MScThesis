using UnityEngine;

public class OneEuroFilter
{
    public float MinCutoff { get; set; }
    public float Beta { get; set; }
    public float DCutoff { get; set; }

    private float lastValue;
    private float lastDeriv;
    private LowPassFilter valueFilter = new LowPassFilter();
    private LowPassFilter derivFilter = new LowPassFilter();

    public OneEuroFilter(float minCutoff = 1f, float beta = 0f, float dCutoff = 1f)
    {
        MinCutoff = minCutoff;
        Beta = beta;
        DCutoff = dCutoff;
    }

    public float Filter(float value, float rate)
    {
        float deriv = (value - lastValue) * rate;
        float ed = derivFilter.Filter(Mathf.Abs(deriv), Alpha(rate, DCutoff));
        float cutoff = MinCutoff + Beta * ed;
        float filtered = valueFilter.Filter(value, Alpha(rate, cutoff));

        lastValue = value;
        lastDeriv = deriv;

        return filtered;
    }

    private float Alpha(float rate, float cutoff)
    {
        float tau = 1f / (2f * Mathf.PI * cutoff);
        return 1f / (1f + tau * rate);
    }
}