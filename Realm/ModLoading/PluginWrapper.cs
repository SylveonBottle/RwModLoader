﻿using BepInEx.Preloader;
using Realm.Gui;
using Realm.Logging;

namespace Realm.ModLoading;

static class PluginWrapper
{
    public static void WrapPlugins(Progressable progressable, out List<string> wrappedMods)
    {
        // IMPORTANT: Do not reference BepInEx, Assembly-CSharp, or UnityEngine in this method. Otherwise, BepInEx won't run the chainloader and Realm won't start.
        // This is why we can't use BepInEx.Paths.PluginPath and why RealmUtils and RealmPaths are separate types.

        wrappedMods = new();

        FileInfo preloaderFile = new(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH));

        string pluginPath = preloaderFile.Directory.Parent.CreateSubdirectory("plugins").FullName;

        if (!Directory.Exists(pluginPath)) {
            return;
        }

        var pluginFiles = Directory.GetFiles(pluginPath, "*.dll", SearchOption.TopDirectoryOnly)
                          .Concat(Directory.GetFiles(pluginPath, "*.zip", SearchOption.TopDirectoryOnly))
                          .Concat(Directory.GetDirectories(pluginPath, "*", SearchOption.TopDirectoryOnly));

        if (!pluginFiles.Any()) {
            return;
        }

        // Create args to pass to backend
        StringBuilder args = new();
        foreach (string pluginFile in pluginFiles) {
            args.Append("-w \"");
            args.Append(pluginFile);
            args.Append("\" ");
        }

        try {
            // Run backend and read its output
            BackendProcess proc = BackendProcess.Execute(args.ToString());

            if (proc.ExitCode == 0) {
                if (proc.Output.Length > 0) {
                    var wrapped = proc.Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    wrappedMods.AddRange(wrapped);

                    progressable.Message(MessageType.Debug, $"Wrapped {wrappedMods.JoinStrEnglish()}");
                }
            }
            else {
                progressable.Message(MessageType.Fatal, $"Failed to wrap {pluginFiles.Select(s => Path.GetFileName(s)).JoinStrEnglish()}");
                progressable.Message(MessageType.Fatal, proc.ToString());
            }
        }
        catch (Exception e) {
            progressable.Message(MessageType.Debug, e.ToString());
            progressable.Message(MessageType.Fatal, "An error occurred while wrapping plugins. Exception details logged.");
        }
    }
}
