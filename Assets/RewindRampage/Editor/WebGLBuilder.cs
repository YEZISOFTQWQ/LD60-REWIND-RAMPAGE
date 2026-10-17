using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class WebGLBuilder
    {
        [MenuItem("Rewind Rampage/Build Windows preview")]
        public static void BuildDesktop()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(project, "Builds/Windows/RewindRampage.exe");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildOutput") output = Path.GetFullPath(args[i + 1]);
            if (!output.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Windows preview must remain inside this project.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows preview export failed: " + report.summary.result);
            Debug.Log("Windows preview exported to " + output + ". No gameplay tests were run.");
        }

        [MenuItem("Rewind Rampage/Build WebGL to docs")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install WebGL Build Support for Unity 2022.3.62f3c1 first.");

            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(project, "docs");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildOutput") output = Path.GetFullPath(args[i + 1]);
            if (!Path.GetFullPath(output).StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WebGL export must remain inside this project.");

            // GitHub Pages does not expose custom Content-Encoding headers.
            // Unity's decompression fallback loads .unityweb files on ordinary static hosting.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:Rewind";
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, Il2CppCompilerConfiguration.Release);
            PlayerSettings.bundleVersion = "0.4.1";
            EditorUserBuildSettings.development = false;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("WebGL export failed: " + report.summary.result);

            File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
            Debug.Log("WebGL exported to " + output + ". Build bytes: " + report.summary.totalSize + ". No gameplay tests were run.");
        }
    }
}
