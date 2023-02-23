using AdaptiveSamplingIBL.Experiments;

SceneRegistry.AddSource("../../../Scenes");

Console.WriteLine("Runing Equal Time Experiment!");


var exp = new EqualTime();

List<(string, int)> scenes = new() {
    ("living-room-2", 5),
    ("dining-room", 5),
    ("kitchen", 5),
};

List<SceneConfig> sceneConfigs = new();
foreach (var (name, maxDepth) in scenes) { 
    sceneConfigs.Add(SceneRegistry.LoadScene(name, maxDepth: maxDepth));
}

new Benchmark(
    new EqualTime(),
    sceneConfigs,
    "Results/deb",
    640, 480,
    frameBufferFlags:SeeSharp.Image.FrameBuffer.Flags.SendToTev
).Run(skipReference:false);