using AdaptiveSamplingIBL.Experiments;

SceneRegistry.AddSource("../../../Scenes");

Console.WriteLine("Runing Equal Time Experiment!");


var exp = new EqualTime();

List<(string, int)> scenes = new() {
    //("living-room", 5),
    ("dining-room", 5),
    ("kitchen", 5),
    ("CornellBox", 5),
};

List<SceneConfig> sceneConfigs = new();
foreach (var (name, maxDepth) in scenes) { 
    sceneConfigs.Add(SceneRegistry.LoadScene(name, maxDepth: maxDepth));
}

new Benchmark(
    new EqualTime(),
    sceneConfigs,
    "Results/EqualTime",
    640, 480
).Run(skipReference:false);