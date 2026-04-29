using UnityEngine;

public class LowPassFilter
{
    private float lastValue;
    private bool isFirst = true;

    public float Filter(float value, float alpha)
    {
        if (isFirst)
        {
            isFirst = false;
            lastValue = value;
            return value;
        }

        float filtered = alpha * value + (1f - alpha) * lastValue;
        lastValue = filtered;
        return filtered;
    }
}