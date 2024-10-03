#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEditor.Build.Reporting;

namespace UnityDriveSimulation
{
    public static class BuildScript
    {
        [MenuItem("Build/Perform macOS Build")]
        public static void PerformMacOSBuild()
        {
            // Set the target architecture to Apple Silicon (ARM64) only ("OSXUniversal" for Universal build (Intel + Apple Silicon))
            EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXArm64", "Architecture", "ARM64");

            PerformBuild(BuildTarget.StandaloneOSX, BuildOptions.None);
        }

        public static void PerformBuild(BuildTarget buildTarget, BuildOptions buildOptions)
        {
            // Enabling Development build and Script Debugging
            buildOptions |= BuildOptions.Development;
            buildOptions |= BuildOptions.AllowDebugging;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string buildPath = GetBuildPath(buildTarget);

            try
            {
                BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = buildTarget,
                    options = buildOptions
                };

                BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                BuildResult result = report.summary.result;

                if (result == BuildResult.Succeeded)
                {
                    Debug.Log($"Build completed successfully: {buildPath}");
                }
                else
                {
                    Debug.LogError($"Build failed with result: {result}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Build failed during build process with exception: {e.Message}");
            }
        }

        private static string GetBuildPath(BuildTarget buildTarget)
        {
            string buildFolder = "Builds/StandaloneOSX";
            string fileName = "libReplicantDriveSim";


            string buildPath = System.IO.Path.Combine(Application.dataPath, $"../{buildFolder}");

            // Create the directory if it doesn't exist
            if (!System.IO.Directory.Exists(buildPath))
            {
                System.IO.Directory.CreateDirectory(buildPath);
                Debug.Log($"Created a build path: {buildPath}");
            }

            Debug.Log($"Build path: {buildPath}");
            Debug.Log($"Current directory: {System.Environment.CurrentDirectory}");

            return System.IO.Path.Combine(buildPath, $"{fileName}.app");
        }
    }
}
#endif
