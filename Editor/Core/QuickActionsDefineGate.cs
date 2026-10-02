// The QUICKACTIONS_ENABLED rules the ungated editor assemblies share: the enable
// menu's token test, and the stale-assembly check both define-off cleanups run.
// This assembly carries no define and no platform constraint, so every assembly
// that has to exist with the define off can reference it.
using UnityEditor;
using UnityEditor.Build;

namespace EminDeniz99.QuickActions.Editor.Core
{
    internal static class QuickActionsDefineGate
    {
        internal const string Define = "QUICKACTIONS_ENABLED";

        internal const string StaleAssembliesMessage =
            "[QuickActions] QUICKACTIONS_ENABLED was removed from the scripting defines, but the editor " +
            "assemblies are still compiled with it (defines changed without a script recompile — e.g. " +
            "SetScriptingDefineSymbols + BuildPlayer inside one batch invocation). This build would still " +
            "contain the dev-only quick-actions pieces. Split the define change and the build into two " +
            "editor invocations (so scripts recompile), or re-add the define. If you supply the define " +
            "only via csc.rsp, mirror it in Player Settings or the active Build Profile so this check can see it.";

        // Exact token match. A substring test would be fooled by an unrelated
        // define that merely contains this one as a prefix or suffix.
        internal static bool HasDefine(string symbols)
        {
            if (string.IsNullOrEmpty(symbols))
                return false;
            foreach (var token in symbols.Split(';'))
            {
                if (token.Trim() == Define)
                    return true;
            }
            return false;
        }

        // For a define-off cleanup whose assembly was compiled WITH the define,
        // where its job is to do nothing. A script that removes the define and
        // builds in the same editor invocation runs with those stale assemblies,
        // so the gated injectors just ran although the caller wanted them gone.
        // Fail the build loudly then instead of shipping dev-only pieces.
        internal static void FailBuildIfDefineRemoved(NamedBuildTarget target)
        {
            if (!EffectiveDefinesContainGate(target))
                throw new BuildFailedException(StaleAssembliesMessage);
        }

        // True when the EFFECTIVE defines still contain the gate: Player Settings
        // for the target, plus the active Unity 6 Build Profile (profiles ADD
        // symbols on top of Player Settings — a dev profile may carry the define
        // alone; read reflectively, the API doesn't exist on 2021/2022 LTS where
        // Player Settings is the whole truth).
        internal static bool EffectiveDefinesContainGate(NamedBuildTarget target)
        {
            if (HasDefine(PlayerSettings.GetScriptingDefineSymbols(target)))
                return true;
            try
            {
                var profileType = System.Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor.CoreModule");
                var getActive = profileType?.GetMethod("GetActiveBuildProfile",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var profile = getActive?.Invoke(null, null);
                if (profile != null)
                {
                    // Unity 6 exposes scriptingDefines as a public FIELD (not a
                    // property) — probe both so a future API change can't silently
                    // blind this check and fail coherent dev-profile builds.
                    object value = profileType.GetProperty("scriptingDefines")?.GetValue(profile)
                        ?? profileType.GetField("scriptingDefines")?.GetValue(profile);
                    if (value is string[] profileDefines)
                        return System.Array.IndexOf(profileDefines, Define) >= 0;
                }
            }
            catch (System.Exception)
            {
                // Reflection shape drifted — fall through to "not found" and let the
                // loud BuildFailedException explain the remedies.
            }
            return false;
        }
    }
}
