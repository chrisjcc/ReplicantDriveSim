#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEditor.Build.Reporting;
using System;
using System.IO;

namespace UnityBuilderAction
{
    [Serializable]
    public class BuildConfiguration
    {
        public int screenWidth = 2560;
        public int screenHeight = 1440;
        public bool isDevelopmentBuild = true;
        public bool allowDebugging = true;
        public bool isResizableWindow = false;
        public FullScreenMode fullScreenMode = FullScreenMode.Windowed;
        public Architecture architecture = Architecture.ARM64;
    }

    public enum Architecture
    {
        ARM64,
        x64,
        Universal
    }

    public static class BuildScript
    {
        [MenuItem("Build/Perform macOS Build (Development)")]
        public static void PerformMacOSBuild()
        {
            var config = new BuildConfiguration
            {
                isDevelopmentBuild = true,
                allowDebugging = true,
                architecture = Architecture.ARM64
            };
            PerformBuild(BuildTarget.StandaloneOSX, BuildOptions.None, config);
        }

        [MenuItem("Build/Perform macOS Build (Release)")]
        public static void PerformMacOSReleaseBuild()
        {
            var config = new BuildConfiguration
            {
                isDevelopmentBuild = false,
                allowDebugging = false,
                architecture = Architecture.ARM64
            };
            PerformBuild(BuildTarget.StandaloneOSX, BuildOptions.None, config);
        }

        [MenuItem("Build/Perform Windows Build (Development)")]
        public static void PerformWindowsBuild()
        {
            var config = new BuildConfiguration
            {
                isDevelopmentBuild = true,
                allowDebugging = true
            };
            PerformBuild(BuildTarget.StandaloneWindows64, BuildOptions.None, config);
        }

        [MenuItem("Build/Perform Windows Build (Release)")]
        public static void PerformWindowsReleaseBuild()
        {
            var config = new BuildConfiguration
            {
                isDevelopmentBuild = false,
                allowDebugging = false
            };
            PerformBuild(BuildTarget.StandaloneWindows64, BuildOptions.None, config);
        }

        [MenuItem("Build/Perform Linux Build (Development)")]
        public static void PerformLinuxBuild()
        {
            var config = new BuildConfiguration
            {
                isDevelopmentBuild = true,
                allowDebugging = true
            };
            PerformBuild(BuildTarget.StandaloneLinux64, BuildOptions.None, config);
        }

        [MenuItem("Build/Perform Linux Build (Release)")]
        public static void PerformLinuxReleaseBuild()
        {
            var config = new BuildConfiguration
            {
                isDevelopmentBuild = false,
                allowDebugging = false
            };
            PerformBuild(BuildTarget.StandaloneLinux64, BuildOptions.None, config);
        }

        private static void ConfigurePlayerSettings(BuildConfiguration config)
        {
            // Configure screen settings
            PlayerSettings.fullScreenMode = config.fullScreenMode;
            PlayerSettings.defaultScreenWidth = config.screenWidth;
            PlayerSettings.defaultScreenHeight = config.screenHeight;
            PlayerSettings.resizableWindow = config.isResizableWindow;

            Debug.Log($"Configured player settings: {config.screenWidth}x{config.screenHeight}, Fullscreen: {config.fullScreenMode}, Resizable: {config.isResizableWindow}");
        }

        private static void ConfigurePlatformSettings(BuildTarget buildTarget, BuildConfiguration config)
        {
            if (buildTarget == BuildTarget.StandaloneOSX)
            {
                string architectureValue = config.architecture switch
                {
                    Architecture.ARM64 => "ARM64",
                    Architecture.x64 => "x64",
                    Architecture.Universal => "OSXUniversal",
                    _ => "ARM64"
                };
                
                string platformId = config.architecture == Architecture.Universal ? "OSXUniversal" : "OSXArm64";
                EditorUserBuildSettings.SetPlatformSettings("Standalone", platformId, "Architecture", architectureValue);
                Debug.Log($"Set macOS architecture to: {architectureValue}");
            }
        }

        public static void PerformBuild(BuildTarget buildTarget, BuildOptions baseOptions, BuildConfiguration config = null)
        {
            // Use default configuration if none provided
            config ??= new BuildConfiguration();

            // Validate inputs
            if (!ValidateBuildInputs(buildTarget, config))
            {
                Debug.LogError("Build validation failed. Aborting build.");
                return;
            }

            // Configure build options based on configuration
            var buildOptions = baseOptions;
            if (config.isDevelopmentBuild)
                buildOptions |= BuildOptions.Development;
            if (config.allowDebugging)
                buildOptions |= BuildOptions.AllowDebugging;

            // Configure player and platform settings
            ConfigurePlayerSettings(config);
            ConfigurePlatformSettings(buildTarget, config);

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
                // Set other options as needed
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

        private static bool ValidateBuildInputs(BuildTarget buildTarget, BuildConfiguration config)
        {
            // Validate build target is supported
            if (!IsSupportedBuildTarget(buildTarget))
            {
                Debug.LogError($"Unsupported build target: {buildTarget}");
                return false;
            }

            // Validate screen resolution
            if (config.screenWidth <= 0 || config.screenHeight <= 0)
            {
                Debug.LogError($"Invalid screen resolution: {config.screenWidth}x{config.screenHeight}");
                return false;
            }

            // Validate scenes
            var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            if (enabledScenes.Length == 0)
            {
                Debug.LogError("No enabled scenes found in build settings.");
                return false;
            }

            return true;
        }

        private static bool IsSupportedBuildTarget(BuildTarget buildTarget)
        {
            return buildTarget == BuildTarget.StandaloneOSX ||
                   buildTarget == BuildTarget.StandaloneWindows64 ||
                   buildTarget == BuildTarget.StandaloneLinux64;
        }

        private static string GetBuildPath(BuildTarget buildTarget)
        {
            string buildFolder = GetBuildFolderName(buildTarget);
            string fileName = "libReplicantDriveSim";
            string extension = GetExecutableExtension(buildTarget);

            string buildPath = Path.Combine(Application.dataPath, $"../{buildFolder}");

            // Create the directory if it doesn't exist
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
                Debug.Log($"Created build directory: {buildPath}");
            }

            Debug.Log($"Build target: {buildTarget}");
            Debug.Log($"Build directory: {buildPath}");

            return Path.Combine(buildPath, $"{fileName}{extension}");
        }

        private static string GetBuildFolderName(BuildTarget buildTarget)
        {
            return buildTarget switch
            {
                BuildTarget.StandaloneOSX => "Builds/StandaloneOSX",
                BuildTarget.StandaloneWindows64 => "Builds/StandaloneWindows64",
                BuildTarget.StandaloneLinux64 => "Builds/StandaloneLinux64",
                _ => throw new ArgumentException($"Unsupported build target: {buildTarget}")
            };
        }

        private static string GetExecutableExtension(BuildTarget buildTarget)
        {
            return buildTarget switch
            {
                BuildTarget.StandaloneOSX => ".app",
                BuildTarget.StandaloneWindows64 => ".exe",
                BuildTarget.StandaloneLinux64 => "",
                _ => ""
            };
        }
    }
}
#endif
