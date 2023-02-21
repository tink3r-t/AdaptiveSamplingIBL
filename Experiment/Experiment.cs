using SeeSharp.Experiments;
using SeeSharp.Integrators;

namespace AdaptiveSamplingIBL.Experiments;

public class EqualTime : Experiment
{
    public override List<Method> MakeMethods()
    {
        List<Method> methods = new() {
            new("PT", new PathTracer() {
                MaximumRenderTimeMs = 60000,
            }),
        };
        return methods;
    }
}
