using SeeSharp.Experiments;
using SeeSharp.Integrators;

namespace AdaptiveSamplingIBL.Experiments;

public class EqualTime : Experiment
{
    public override List<Method> MakeMethods()
    {
        List<Method> methods = new() {
            new("PT", new PathTracer() {
                MaximumRenderTimeMs = 30000,
                TotalSpp = 100,
            }),
            new("AdaptiveSampler-ES", new AdaptiveSampling() {
                TilerType = AdaptiveSampling.TilerTypes.EqualSize,
                MaximumRenderTimeMs = 30000,
                TotalSpp = 100,
            }),
            new("AdaptiveSampler-EE", new AdaptiveSampling() {
                TilerType = AdaptiveSampling.TilerTypes.EqualEnergy,
                MaximumRenderTimeMs = 30000,
                TotalSpp = 100,
            }),
            new("AdaptiveSampler-AD", new AdaptiveSampling() {
                TilerType = AdaptiveSampling.TilerTypes.Adaptive,
                MaximumRenderTimeMs = 30000,
                TotalSpp = 100,
            }),
        };
        return methods;
    }
}
