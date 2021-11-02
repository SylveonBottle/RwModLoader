﻿using Realm.AssemblyLoading;
using Realm.Logging;

namespace Realm.ModLoading;

sealed class ModLoader
{
    public LoadedAssemblyPool? LoadedAssemblyPool { get; private set; }

    public void Unload(IProgressable progressable)
    {
        progressable.Message(MessageType.Info, "Disabling mods");

        LoadedAssemblyPool?.Unload(progressable);
        LoadedAssemblyPool = null;
    }

    public void Reload(IProgressable progressable)
    {
        List<ModReloadState> reloadState = GetReloadState(progressable);

        Unload(progressable);

        PluginWrapper.WrapPlugins(progressable, out var wrappedAsms);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        if (wrappedAsms.Count > 0) {
            State.Instance.Prefs.Enable(wrappedAsms);
            State.Instance.Prefs.Save();
        }

        progressable.Message(MessageType.Info, "Reading assemblies");

        List<RwmodFile> plugins = new();

        // DO NOT inline this variable. Obviously.
        // Not like anyone would do that.
        RwmodFile[] rwmods = RwmodFile.GetRwmodFiles();

        // Ensure that these streams get disposed after we're done using them.
        using Disposable disposeStreams = new(() => { foreach (var r in rwmods) r.Stream.Dispose(); });

        foreach (var rwmod in rwmods) {
            if (State.Instance.Prefs.EnabledMods.Contains(rwmod.Header.Name)) {
                plugins.Add(rwmod);
            }
        }

        AssemblyPool assemblyPool = AssemblyPool.Read(progressable, plugins);

        if (progressable.ProgressState == ProgressStateType.Failed) return;
        progressable.Message(MessageType.Info, "Loading assemblies");
        progressable.Progress = 0;

        LoadedAssemblyPool = LoadedAssemblyPool.Load(progressable, assemblyPool);

        if (progressable.ProgressState == ProgressStateType.Failed) return;
        progressable.Message(MessageType.Info, "Enabling mods");
        progressable.Progress = 0;

        LoadedAssemblyPool.InitializeMods(progressable);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        // Call Reload after the mods are loaded
        // This ensures they have an instance to pass the state to
        CallReload(progressable, reloadState);
    }

    private void CallReload(IProgressable progressable, List<ModReloadState> reloadState)
    {
        foreach (var state in reloadState) {
            LoadedModAssembly? newMod = LoadedAssemblyPool!.LoadedAssemblies.FirstOrDefault(m => m.AsmName == state.AsmName);

            if (newMod != null) {
                try {
                    LoadedAssemblyPool.Pool[state.AsmName].Descriptor.SetUnloadState(state.ModData);
                } catch (Exception e) {
                    progressable.Message(MessageType.Fatal, $"An uncaught exception was thrown in {newMod.AsmName}. {e}");
                }
            }
        }
    }

    private List<ModReloadState> GetReloadState(IProgressable progressable)
    {
        if (LoadedAssemblyPool == null) {
            return new();
        }

        List<ModReloadState> ret = new();

        foreach (var asm in LoadedAssemblyPool.LoadedAssemblies) {
            try {
                var state = LoadedAssemblyPool.Pool[asm.AsmName].Descriptor.GetReloadState();
                if (state is not null) {
                    ret.Add(new() { AsmName = asm.AsmName, ModData = state.Value });
                }
            } catch (Exception e) {
                progressable.Message(MessageType.Fatal, $"An uncaught exception was thrown in {asm.AsmName}. {e}");
            }
        }

        return ret;
    }
}

struct ModReloadState
{
    public string AsmName;
    public object? ModData;
}
