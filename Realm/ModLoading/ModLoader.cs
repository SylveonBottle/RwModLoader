﻿using Realm.AssemblyLoading;
using Realm.Logging;
using Rwml;

namespace Realm.ModLoading;

sealed class ModLoader
{
    public LoadedAssemblyPool? LoadedAssemblyPool { get; private set; }

    public void UnloadAndQuit(Progressable progressable)
    {
        GetReloadState(progressable);
        Unload(progressable);
        UnityEngine.Application.Quit();
    }

    public void Reload(Progressable progressable)
    {
        List<ModReloadState> reloadState = GetReloadState(progressable);

        Unload(progressable);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        Load(progressable, reloadState);
    }

    private void Unload(Progressable progressable)
    {
        progressable.Message(MessageType.Info, "Disabling mods");

        if (LoadedAssemblyPool == null) return;

        LoadedAssemblyPool.Unload(progressable);
        LoadedAssemblyPool = null;
    }

    private void Load(Progressable progressable, List<ModReloadState> reloadState)
    {
        PluginWrapper.WrapPlugins(progressable, out var wrappedAsms);

        if (progressable.ProgressState == ProgressStateType.Failed) return;

        if (wrappedAsms.Count > 0) {
            State.Prefs.Enable(wrappedAsms);
            State.Prefs.Save();
        }

        progressable.Message(MessageType.Info, "Reading assemblies");

        List<RwmodFile> plugins = new();

        // DO NOT inline this variable.
        var rwmodsR = RwmodFile.GetRwmodFiles();

        if (rwmodsR.MatchFailure(out var rwmods, out var err)) {
            progressable.Message(MessageType.Fatal, err);
            return;
        }

        using Disposable disposeStreams = new(() => rwmods.ForEach(r => r.Stream.Dispose()));

        foreach (var rwmod in rwmods) {
            if (State.Prefs.EnabledMods.Contains(rwmod.Header.Name)) {
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

    private void CallReload(Progressable progressable, List<ModReloadState> reloadState)
    {
        foreach (var state in reloadState) {
            LoadedModAssembly? newMod = LoadedAssemblyPool!.LoadedAssemblies.FirstOrDefault(m => m.AsmName == state.AsmName);

            if (newMod != null) {
                try {
                    LoadedAssemblyPool.Pool[state.AsmName].Descriptor.SetUnloadState(state.ModData);
                }
                catch (Exception e) {
                    progressable.Message(MessageType.Fatal, $"An uncaught exception was thrown in {newMod.AsmName}. {e}");
                }
            }
        }
    }

    private List<ModReloadState> GetReloadState(Progressable progressable)
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
            }
            catch (Exception e) {
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
