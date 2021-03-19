using Vintagestory.API.Client;
using Vintagestory.API.Common;
using HarmonyLib;

namespace Shaders
{
    public class Parallax : IRenderer
    {
        ShadersMod mod;

        public Parallax(ShadersMod mod)
        {
            this.mod = mod;
        }

        public double RenderOrder => 1;

        public int RenderRange => int.MaxValue;

        public void Dispose()
        {
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
        }
    }

    public class ShadersMod : ModSystem
    {
        public static ShadersMod Instance { get; private set; }
        public static ModSettings Settings { get; private set; }

        public ICoreClientAPI capi { get; private set; }
        public Events Events { get; private set; }

        public ShaderInjector ShaderInjector { get; private set; }
        public ScreenSpaceReflections ScreenSpaceReflections { get; private set; }
        public VolumetricLighting VolumetricLighting { get; private set; }
        public OverexposureEffect OverexposureEffect { get; private set; }
        public ScreenSpaceDirectionalOcclusion ScreenSpaceDirectionalOcclusion { get; private set; }
        public ShadowTweaks ShadowTweaks { get; private set; }
        public Parallax Parallax { get; private set; }

        public ConfigGui ConfigGui;
        public GuiDialog CurrentDialog;

        private Harmony _harmony;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            PatchGame();
            RegisterHotkeys();

            Settings = new ModSettings(api);
            VolumetricLighting = new VolumetricLighting(this);
            ScreenSpaceReflections = new ScreenSpaceReflections(this);
            OverexposureEffect = new OverexposureEffect(this);
            ScreenSpaceDirectionalOcclusion = new ScreenSpaceDirectionalOcclusion(this);
            ShadowTweaks = new ShadowTweaks(this);
        }

        public override void StartPre(ICoreAPI api)
        {
            if (api is ICoreClientAPI)
            {
                Instance = this;
                capi = (ICoreClientAPI)api;
                Events = new Events();

                ShaderInjector = new ShaderInjector();
            }
        }

        private void RegisterHotkeys()
        {
            capi.Input.RegisterHotKey("volumetriclightingconfigure", "Volumetric Lighting Configuration", GlKeys.C,
                HotkeyType.GUIOrOtherControls, ctrlPressed: true);
            capi.Input.SetHotKeyHandler("volumetriclightingconfigure", OnConfigurePressed);
        }

        private void PatchGame()
        {
            Mod.Logger.Event("Loading harmony for patching...");
            _harmony = new Harmony("com.xxmicloxx.vsvolumetricshading");
            _harmony.PatchAll();

            var myOriginalMethods = _harmony.GetPatchedMethods();
            foreach (var method in myOriginalMethods)
            {
                Mod.Logger.Event("Patched " + method.FullDescription());
            }
        }

        private bool OnConfigurePressed(KeyCombination cb)
        {
            if (ConfigGui == null)
            {
                ConfigGui = new ConfigGui(capi);
            }

            if (CurrentDialog != null && CurrentDialog.IsOpened())
            {
                CurrentDialog.TryClose();
                return true;
            }

            ConfigGui.TryOpen();
            return true;
        }

        public override void Dispose()
        {
            if (capi == null) return;

            _harmony?.UnpatchAll();

            Instance = null;
            SkyVisibility.initialized = false;
        }
    }
}