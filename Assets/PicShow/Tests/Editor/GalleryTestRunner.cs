using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PicShow.Tests
{
    public static class GalleryTestRunner
    {
        private static TestRunnerApi runner;

        public static void Run(string outputPath)
        {
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Results(outputPath));
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "PicShow.Editor.Tests" }
            }));
        }

        private sealed class Results : ICallbacks
        {
            private readonly string path;
            public Results(string path) => this.path = path;
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, path);
                Debug.Log($"PicShow tests: {result.PassCount} passed, {result.FailCount} failed. Results: {Path.GetFullPath(path)}");
                Object.DestroyImmediate(runner);
            }
        }
    }
}
