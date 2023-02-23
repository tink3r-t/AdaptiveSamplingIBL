using System.Threading;
using SeeSharp.Integrators.Util;
using SeeSharp.Shading.Background;

namespace AdaptiveSamplingIBL;

/// <summary>
/// A classic path tracer with next event estimation
/// </summary>
public class AdaptiveSampling : PathTracer
{

    const float EPSILION = 5.8504527E-04f;

    /// <summary>
    /// No of luminance cache bins in Axis X.
    /// </summary>
    public short LightGridX = 100;
    /// <summary>
    /// No of luminance cache bins in Axis Y.
    /// </summary>
    public short LightGridY = 50;

    /// <summary>
    /// No of blocks if equal engery tiles in Axis X.
    /// </summary>
    public short EnvironmentSpaceX = 32;
    /// <summary>
    ///  No of blocks if equal engery tiles in Axis Y.
    /// </summary>
    public short EnvironmentSpaceY = 16;

    /// <summary>
    /// Different tiles of Environment.
    /// </summary>
    Tiler environmentTiler;

    /// <summary>
    /// Cached Weights of tiles
    /// </summary>
    (float, int)[,,] tileWeights;

    /// <summary>
    /// Environment Tiles sampling probability from each light tile. 
    /// </summary>
    PiecewiseConstant[,] learnedCache;

    /// <summary>
    /// No of Rays to sample cache during learning phase.
    /// </summary>
    public int? LearningRays = 1000000;

    public enum TilerTypes { 
        EqualSize,
        EqualEnergy,
        Adaptive
    }

    /// <summary>
    /// 3 Types currently supported
    ///  Equal Size
    ///  Equal Engergy
    ///  Adaptive
    /// </summary>
    public TilerTypes TilerType = TilerTypes.EqualSize;

    /// <summary> Called after the scene was submitted, before rendering starts. </summary>
    protected override void OnPrepareRender()
    {
        learnedCache = new PiecewiseConstant[LightGridX, LightGridY];
        switch (TilerType)
        {
            case TilerTypes.EqualEnergy:
                environmentTiler = new EqualEnergyTiler(((EnvironmentMap)scene.Background).Image, EnvironmentSpaceX, EnvironmentSpaceY);
                break;
            case TilerTypes.Adaptive:
                environmentTiler = new AdaptiveTiler(((EnvironmentMap)scene.Background).Image, EnvironmentSpaceX, EnvironmentSpaceY);
                break;
            case TilerTypes.EqualSize:
                environmentTiler = new EqualSizeTiler(((EnvironmentMap)scene.Background).Image, EnvironmentSpaceX, EnvironmentSpaceY);
                break;
        }
        tileWeights = new (float, int)[LightGridX, LightGridY, environmentTiler.GetTilesCount()];

        for (int i = 0; i < LightGridX; i++)
            for (int j = 0; j < LightGridY; j++)
                for (int k = 0; k < environmentTiler.GetTilesCount(); k++)
                {
                    tileWeights[i, j, k].Item1 = EPSILION;
                    tileWeights[i, j, k].Item2 = 1;
                }
    }

    Vector2 WorldToSpherical(Vector3 dir)
    {
        dir = Vector3.Normalize(dir);
        var sp = new Vector2(
            MathF.Atan2(dir.Z, dir.X),
            MathF.Atan2(MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z), dir.Y)
        );
        if (sp.X < 0) sp.X += MathF.PI * 2.0f;
        return sp;
    }

    Vector3 SphericalToWorld(Vector2 spherical)
    {
        float sinTheta = MathF.Sin(spherical.Y);
        return new Vector3(
            sinTheta * MathF.Cos(spherical.X),
            MathF.Cos(spherical.Y),
            sinTheta * MathF.Sin(spherical.X)
        );
    }

    Vector2 SphericalToPixel(Vector2 sphericalDir) => new Vector2(sphericalDir.X / (2 * MathF.PI), sphericalDir.Y / MathF.PI);

    Vector2 PixelToSpherical(Vector2 pixel) => new Vector2(pixel.X * 2 * MathF.PI, pixel.Y * MathF.PI);

    Vector3 PixelToWorld(Vector2 pixel)
    {
        var spherical = PixelToSpherical(pixel);
        return SphericalToWorld(spherical);
    }

    Vector2 WorldToPixel(Vector3 dir)
    {
        var spherical = WorldToSpherical(dir);
        return SphericalToPixel(spherical);
    }

    /// <summary>
    /// Apply sharpening
    /// </summary>
    public bool MISOptimization = false;


    /// <summary>
    /// Luminance learning phase to be used for importance sampling while rendering
    /// </summary>
    private void LearningPhase()
    {
        RNG randomPixelG = new RNG(BaseSeed);
        if (!LearningRays.HasValue)
        {
            LearningRays = environmentTiler.GetTilesCount() * LightGridX * LightGridY * 10;
        }

#if DEBUG
        for (int i = 0; i < LearningRays; i++)
        {
#else
        Parallel.For(0, LearningRays.GetValueOrDefault(), n => {
#endif

            Vector2 randomPixel = randomPixelG.NextFloat2D();
            uint pixelIndex = (uint)(randomPixel.X * scene.FrameBuffer.Width * scene.FrameBuffer.Height + randomPixel.Y * scene.FrameBuffer.Height);
            RNG randomGenerator = new(BaseSeed, pixelIndex, 1);
            Ray ray = new Ray
            {
                Origin = scene.Camera.Position,
                MinDistance = 0.0f,
                Direction = PixelToWorld(randomPixel)
            };

            ray.Direction = Vector3.Normalize(ray.Direction);

            PathState state = new()
            {
                Pixel = randomPixel,
                Rng = randomGenerator,
                Depth = 1
            };
            CacheTilesVisbility(ray, ref state);
#if DEBUG
        }
#else
        });
#endif
        float[] allValues = new float[environmentTiler.GetTilesCount()];
        //Create CFD from cached Visbility
        for (int i = 0; i < LightGridX; i++)
        {
            for (int j = 0; j < LightGridY; j++)
            {
                double totalMagnitude = 0;
                for (int k = 0; k < environmentTiler.GetTilesCount(); k++)
                {
                    float val = environmentTiler.GetTileMagnitude(k) * (tileWeights[i, j, k].Item1 / tileWeights[i, j, k].Item2);
                    allValues[k] = val;

                    totalMagnitude += val;
                    
                }
                if (MISOptimization)
                {
                    double average = totalMagnitude / (environmentTiler.GetTilesCount());
                    float val = (float)average;
                    for (int k = 0; k < environmentTiler.GetTilesCount(); k++)
                    {
                        allValues[k] = MathF.Max(allValues[k] - val, 0.0f);
                    }
                }
                learnedCache[i, j] = new PiecewiseConstant(allValues);
            }
        }

    }

    private (int, int) GetLightGrid(Vector3 direction)
    {
        var coords = WorldToPixel(direction);
        return (Math.Min((int)(coords.X * (LightGridX)), LightGridX - 1), Math.Min((int)(coords.Y * (LightGridY)), LightGridY - 1));
    }

    private int GetEnvTile(Vector3 direction)
    {
        var coords = WorldToPixel(direction);
        return environmentTiler.GetTileIndexByWorldPos(coords);
    }

    private (float, (float, float)) GetLearnedProbabiltyDirection(Vector3 hitpoint, Vector3 direction)
    {
        var directionlight = hitpoint - scene.Camera.Position;
        directionlight = Vector3.Normalize(directionlight);
        var coords = WorldToPixel(directionlight);
        var lightGrid = GetLightGrid(directionlight);

        var pixelCoordinates = WorldToPixel(direction);

        var envPixel = environmentTiler.worldPixelToLocalPixel(pixelCoordinates);

        Debug.Assert(envPixel.tileNo < EnvironmentSpaceX * EnvironmentSpaceY);

        float pdftile = learnedCache[lightGrid.Item1, lightGrid.Item2].Probability(envPixel.tileNo); 

        float localPixelInTilePdf = environmentTiler.GetPointProbabilty(envPixel);

        var pdf = pdftile * localPixelInTilePdf * environmentTiler.GetTilesCount();

        var sphericalDir = WorldToSpherical(direction);

        float jacobian = MathF.Sin(sphericalDir.Y) * MathF.PI * MathF.PI * 2.0f;
        if (jacobian == 0.0f)
        {
            return (0, (0, 0));
        }
        pdf /= jacobian;

        return (pdf, (envPixel.offset.X, envPixel.offset.Y));

    }

    /// <summary>
    /// Debuging - Cache BSDF hits
    /// </summary>
    public bool cacheBSDF = true;
    /// <summary>
    /// Debuging - cache NEE hits
    /// </summary>
    public bool cacheNEE = true;

    void CacheTilesVisbility(in Ray ray, ref PathState state)
    {

        var pos = scene.Camera.Position;
        // Trace the next ray
        if (state.Depth > 4) //Reduce learning time in highly occluded scenes
            return;

        SurfacePoint hit = scene.Raytracer.Trace(ray);

        if (!hit)
        {

            if (state.PreviousHit.HasValue)
            {
                var direction = state.PreviousHit.GetValueOrDefault().Position - pos;
                var lightGrid = GetLightGrid(direction);
                var envGrid = GetEnvTile(ray.Direction);
                float lum = scene.Background.EmittedRadiance(ray.Direction).Luminance;

#if DEBUG
                if (!cacheBSDF)
                {
                    return;
                }
#endif
                Atomic.AddFloat(ref tileWeights[lightGrid.Item1, lightGrid.Item2, envGrid].Item1, lum); // 💜
                Interlocked.Increment(ref tileWeights[lightGrid.Item1, lightGrid.Item2, envGrid].Item2); // 
                //tileWeights[lightGrid.Item1, lightGrid.Item2, envGrid].Item1 += lum; // 
                //tileWeights[lightGrid.Item1, lightGrid.Item2, envGrid].Item2++; // 
                return;
            }
            else
            {
                return; //Direct Hit 
            }

        }
        var direction2 = hit.Position - pos;
        direction2 = Vector3.Normalize(direction2);
#if DEBUG
        if (cacheNEE && state.Depth > MinDepth && state.Depth < MaxDepth)
        {
#else
        if (state.Depth > MinDepth && state.Depth < MaxDepth){
#endif
            //nextEventContrib += PerformBackgroundNextEvent(ray, hit, state);
            var sample = scene.Background.SampleDirection(state.Rng.NextFloat2D());
            if (scene.Raytracer.LeavesScene(hit, sample.Direction))
            {
                var lightGrid = GetLightGrid(direction2);
                var envGrid = GetEnvTile(sample.Direction);
                if ((Vector3.Dot(hit.ShadingNormal, sample.Direction) > 0 && Vector3.Dot(hit.ShadingNormal, ray.Direction) < 0) || (Vector3.Dot(hit.ShadingNormal, sample.Direction) < 0 && Vector3.Dot(hit.ShadingNormal, ray.Direction) > 0))
                {
                    float lumi = scene.Background.EmittedRadiance(ray.Direction).Luminance;
                    Atomic.AddFloat(ref tileWeights[lightGrid.Item1, lightGrid.Item2, envGrid].Item1, lumi);   //💚
                    Interlocked.Increment(ref tileWeights[lightGrid.Item1, lightGrid.Item2, envGrid].Item2);
                    //tileWeights[lightGrid.Item1, lightGrid.Item2, envGrid].Item1 += lumi;   
                    //tileWeights[lightGrid.Item1, lightGrid.Item2, envGrid].Item2++;   

                }
            }
        }

        Vector2 primary = state.Rng.NextFloat2D();
        var bsdfSample = hit.Material.Sample(hit, -ray.Direction, false, primary);
        var bsdfRay = Raytracer.SpawnRay(hit, bsdfSample.direction);

        if (bsdfSample.pdf == 0)
            return;

        // Recursively estimate the incident radiance and log the result
        state.Depth++;
        state.PreviousHit = hit;
        CacheTilesVisbility(bsdfRay, ref state);

    }

    public bool CountLearningTime = true;

    /// <summary>
    /// Renders a scene with the current settings. Only one scene can be rendered at a time.
    /// </summary>
    public override void Render(Scene scene)
    {
        this.scene = scene;

        OnPrepareRender();

        // Add custom frame buffer layers
        if (EnableDenoiser)
            denoiseBuffers = new(scene.FrameBuffer);

        if (!CountLearningTime)
            LearningPhase();

        ProgressBar progressBar = new(prefix: "Rendering...");
        progressBar.Start(TotalSpp);
        RenderTimer timer = new();

        if (CountLearningTime)
            LearningPhase();

        for (uint sampleIndex = 0; sampleIndex < TotalSpp; ++sampleIndex)
        {
            long nextIterTime = timer.RenderTime + timer.PerIterationCost;
            if (MaximumRenderTimeMs.HasValue && nextIterTime > MaximumRenderTimeMs.Value)
            {
                Logger.Log("Maximum render time exhausted.");
                if (EnableDenoiser) denoiseBuffers.Denoise();
                break;
            }
            timer.StartIteration();

            scene.FrameBuffer.StartIteration();
            timer.EndFrameBuffer();

            OnPreIteration(sampleIndex);
            Parallel.For(0, scene.FrameBuffer.Height, row => {
                for (uint col = 0; col < scene.FrameBuffer.Width; ++col)
                {
                    uint pixelIndex = (uint)(row * scene.FrameBuffer.Width + col);
                    RNG rng = new(BaseSeed, pixelIndex, sampleIndex);
                    RenderPixel((uint)row, col, rng);
                }
            });
            OnPostIteration(sampleIndex);
            timer.EndRender();

            if (sampleIndex == TotalSpp - 1)
            {
                if (EnableDenoiser) denoiseBuffers.Denoise();
            }
            scene.FrameBuffer.EndIteration();
            timer.EndFrameBuffer();

            progressBar.ReportDone(1);
            timer.EndIteration();

        }

        scene.FrameBuffer.MetaData["RenderTime"] = timer.RenderTime;
        scene.FrameBuffer.MetaData["FrameBufferTime"] = timer.FrameBufferTime;
    }

    private void RenderPixel(uint row, uint col, RNG rng)
    {
        // Sample a ray from the camera
        var offset = rng.NextFloat2D();
        var pixel = new Vector2(col, row) + offset;
        Ray primaryRay = scene.Camera.GenerateRay(pixel, rng).Ray;

        PathState state = new()
        {
            Pixel = pixel,
            Rng = rng,
            Throughput = RgbColor.White,
            Depth = 1
        };

        OnStartPath(state);
        var estimate = EstimateIncidentRadiance(primaryRay, ref state);
        OnFinishedPath(estimate, state);

        scene.FrameBuffer.Splat(col, row, estimate.Outgoing);
    }

    protected override (RgbColor,float) OnBackgroundHit(in Ray ray, in PathState state)
    {

        if (scene.Background == null || !EnableBsdfDI)
            return (RgbColor.Black, 0);

        float misWeight = 1.0f;
        float pdfNextEvent = 0;
        if (state.Depth > 1)
        {
            // Compute the balance heuristic MIS weight
            pdfNextEvent = GetLearnedProbabiltyDirection(ray.Origin, ray.Direction).Item1 * NumShadowRays;
            misWeight = 1 / (1 + pdfNextEvent / state.PreviousPdf);
        }

        var emission = scene.Background.EmittedRadiance(ray.Direction);
        RegisterSample(state.Pixel, emission * state.Throughput, misWeight, state.Depth, false);
        OnHitLightResult(ray, state, misWeight, emission, true);
        return (misWeight * emission, pdfNextEvent);

    }

    protected override RgbColor PerformBackgroundNextEvent(in Ray ray, in SurfacePoint hit, in PathState state)
    {
        if (scene.Background == null)
            return (RgbColor.Black); // There is no background

        BackgroundSample sample;

        var directionlight = hit.Position - scene.Camera.Position;
        directionlight = Vector3.Normalize(directionlight);
        var lightGrid = GetLightGrid(directionlight);

        var rand = state.Rng.NextFloat();
        var tile = learnedCache[lightGrid.Item1, lightGrid.Item2].Sample(rand);
        var pdfTile = learnedCache[lightGrid.Item1, lightGrid.Item2].Probability(tile.BinIndex);
        // var tileIndex = learnedCache[lightGrid.Item1, lightGrid.Item2].SampleIndex(rand);

        var primary = state.Rng.NextFloat2D();


        //BackgroundSample sample
        var tileSample = environmentTiler.SampleInTile(tile.BinIndex, primary);

        var pdf = tileSample.PdfInTile * pdfTile * environmentTiler.GetTilesCount();
        // Warp the pixel coordinates to the sphere of directions.
        var sphericalDir = PixelToSpherical(tileSample.worldpixel);
        var direction = SphericalToWorld(sphericalDir);

        // TODO we could (and should) pre-multiply the pdf by the sine, to avoid oversampling regions that will receive zero weight
        float jacobian = MathF.Sin(sphericalDir.Y) * MathF.PI * MathF.PI * 2.0f;
        if (jacobian == 0.0f)
        {
            return RgbColor.Black;
        }
        pdf /= jacobian;

        // Compute the sample weight
        var weight = ((EnvironmentMap)scene.Background).Image.GetPixel(
            (int)(tileSample.worldpixel.X * ((EnvironmentMap)scene.Background).Image.Width),
            (int)(tileSample.worldpixel.Y * ((EnvironmentMap)scene.Background).Image.Height)
        ) / pdf;


        sample = new BackgroundSample
        {
            Direction = direction,
            Pdf = pdf,
            Weight = weight
        };

        if (scene.Raytracer.LeavesScene(hit, sample.Direction))
        {
            var bsdfTimesCosine = hit.Material.EvaluateWithCosine(
                hit, -ray.Direction, sample.Direction, false);
            var pdfBsdf = DirectionPdf(hit, -ray.Direction, sample.Direction, state);
            var pdfNEE = ((EnvironmentMap)scene.Background).DirectionPdf(sample.Direction) * NumShadowRays;
            // Prevent NaN / Inf
            if (pdfBsdf == 0 || sample.Pdf == 0)
                return (RgbColor.Black);

            if (!EnableBsdfDI)
                pdfBsdf = 0;

            float misWeight = 1 / (1.0f + pdfBsdf / (sample.Pdf * NumShadowRays));
            var contrib = sample.Weight * bsdfTimesCosine / NumShadowRays;

            Debug.Assert(float.IsFinite(contrib.Average));
            Debug.Assert(float.IsFinite(misWeight));

            RegisterSample(state.Pixel, contrib * state.Throughput, misWeight, state.Depth + 1, true);
            OnNextEventResult(ray, hit, state, misWeight, contrib);

            return (misWeight * contrib);
        }
        return (RgbColor.Black);
    }   
}