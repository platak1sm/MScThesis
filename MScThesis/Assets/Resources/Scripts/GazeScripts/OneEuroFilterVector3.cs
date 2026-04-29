using UnityEngine;

public class OneEuroFilterVector3
{
    private OneEuroFilter xFilter;
    private OneEuroFilter yFilter;
    private OneEuroFilter zFilter;

    public OneEuroFilterVector3(float minCutoff, float beta, float dCutoff)
    {
        xFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
        yFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
        zFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
    }

    public Vector3 Filter(Vector3 value, float rate)
    {
        return new Vector3(
            xFilter.Filter(value.x, rate),
            yFilter.Filter(value.y, rate),
            zFilter.Filter(value.z, rate)
        );
    }
}