using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Reflection;
using FrooxEngine;
using Elements.Core;
using System.Security.Cryptography.X509Certificates;
using System.Drawing;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Time;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Operators;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Color;
using FrooxEngine.ProtoFlux;
using FrooxEngine.ProtoFlux.CoreNodes;

namespace UiBackFlare;

public class UiBackFlare : ResoniteMod
{
    public override string Name => "Ui Back Flare";
    public override string Author => "EuphieEuphoria";
    public override string Version => "0.0.1";
    public override string Link => "https://github.com/EuphieEuphoria/Ui-Back-Flare";
    public static ModConfiguration? Config;

    //default assets
    static Uri DefaultInspectorBackFlareUri = new Uri("resdb:///ed174a955b24e207f984806f4e1fd3b0a3a6e29088f1e6d56caa7e06bb07c737.png");

    static Uri DefaultFluxBackFlareUri = new Uri("resdb:///995a4a899350ee6a3ccd87116f305b770079072a86bd36cce4d04bec2a6b0903.png");

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> BackFlareEnabled = new("BackFlareEnabled", "Enable Back Flare", () => true);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> ColorShiftEnabled = new("ColorShiftEnabled", "Enable Color Shift", () => true);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<Uri> InspectorBackFlareUri = new ("InspectorBackFlareUri", "Inspector Back Flare Uri", () => DefaultInspectorBackFlareUri);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<Uri> FluxBackFlareUri = new ("FluxBackFlareUri", "Flux Browser Back Flare Uri", () => DefaultFluxBackFlareUri);

    public override void OnEngineInit()
    {
        Harmony harmony = new("net.You.UiBackFlare");
        Config = GetConfiguration();
        Config!.Save(true);
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(SceneInspector), "OnAttach")]
    class SceneInspectorPatch {
        static void Postfix(SceneInspector __instance) {
            if (Config!.GetValue(BackFlareEnabled)) {
                __instance.Slot.Name = "Inspector";
                generateFlare(Config!.GetValue(InspectorBackFlareUri) ?? DefaultInspectorBackFlareUri, new float2(800f, 800f), __instance.Slot);
            }
        }
    }

    [HarmonyPatch(typeof(ComponentSelector), "SetupUI")]
    class FluxBrowserPatch {
        static void Postfix(ComponentSelector __instance, Sync<string> ____rootPath) {
            if (Config!.GetValue(BackFlareEnabled)) {
                __instance.RunInUpdates(0, () =>
                {
                    var slot = __instance.Slot;
                    if (____rootPath.Value == ProtoFluxHelper.PROTOFLUX_ROOT)
                    {
                        slot.Name = "Node Browser";
                        generateFlare(Config!.GetValue(FluxBackFlareUri) ?? DefaultFluxBackFlareUri, new float2(400f, 400f), slot);
                    }
                }
                );
            }
        }
    }


    public static void generateFlare(Uri flareUri, float2 size, Slot slot) {
        Slot assets = slot.AddSlot("Assets");
        assets.Tag = "Inspector.assets";
        Slot backFlare = slot.AddSlot("BackFlare");
        backFlare.Tag = "Developer";

        StaticTexture2D inspectorBackFlareTex = assets.AttachComponent<StaticTexture2D>(true, null);
        inspectorBackFlareTex.URL.Value = flareUri;
        inspectorBackFlareTex.FilterMode.Value = TextureFilterMode.Anisotropic;
        inspectorBackFlareTex.AnisotropicLevel.Value = 16;
        UnlitMaterial backFlareUnlitMat = assets.AttachComponent<UnlitMaterial>(true, null);
        backFlareUnlitMat.Texture.Target = inspectorBackFlareTex;
        backFlareUnlitMat.TintColor.Value = new colorX(1.25f, 1.25f, 1.25f, 1f);
        backFlareUnlitMat.BlendMode.Value = BlendMode.Alpha;
        backFlareUnlitMat.Sidedness.Value = Sidedness.Back;

        QuadMesh backFlareQuadMesh = backFlare.AttachMesh<QuadMesh>(backFlareUnlitMat, false, 0);
        backFlareQuadMesh.Size.Value = size;

        if (Config!.GetValue(ColorShiftEnabled)) {
            Slot colorDriver = backFlare.AddSlot("ColorDriver");

            List<IField<colorX>> colorTargets = new List<IField<colorX>>();
            colorTargets.Add(backFlareQuadMesh.UpperLeftColor);
            colorTargets.Add(backFlareQuadMesh.LowerLeftColor);
            colorTargets.Add(backFlareQuadMesh.LowerRightColor);
            colorTargets.Add(backFlareQuadMesh.UpperRightColor);

            var T = colorDriver.AttachComponent<WorldTimeFloat>();
            var TMulti = colorDriver.AttachComponent<ValueMul<float>>();
            var TMultiValue = colorDriver.AttachComponent<ValueInput<float>>();
            TMultiValue.Value.Value = .25f;

            var colorRot = .25f;

            var colorSaturation = colorDriver.AttachComponent<ValueInput<float>>();
            colorSaturation.Value.Value = .75f;

            var colorValue = colorDriver.AttachComponent<ValueInput<float>>();
            colorValue.Value.Value = 1f;

            TMulti.A.Target = T;
            TMulti.B.Target = TMultiValue;

            for (int i =0; i< colorTargets.Count; i++) {
                var colorRotHolder = colorDriver.AttachComponent<ValueInput<float>>();
                colorRotHolder.Value.Value = colorRot * i;
                var addition = colorDriver.AttachComponent<ValueAdd<float>>();
                addition.A.Target = TMulti;
                addition.B.Target = colorRotHolder;
                var hsv = colorDriver.AttachComponent<HSV_ToColorX>();
                hsv.H.Target = addition;
                hsv.S.Target = colorSaturation;
                hsv.V.Target = colorValue;
                var driver = (ProtoFluxNode)colorDriver.AttachComponent(ProtoFluxHelper.GetDriverNode(typeof(colorX)));
                driver.TryConnectInput(driver.GetInput(0), hsv.GetOutput(0), false, false);
                ((IDrive)driver).TrySetRootTarget(colorTargets[i]);
            }
        }
    }
}
