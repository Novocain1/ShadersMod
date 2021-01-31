using System.Reflection;
using Vintagestory.Client.NoObf;

namespace Shaders
{
    public class VolumetricLighting
    {
        private readonly ShadersMod mod;

        public VolumetricLighting(ShadersMod mod)
        {
            this.mod = mod;

            this.mod.capi.Settings.AddWatcher<int>("shadowMapQuality", OnShadowMapChanged);
            this.mod.capi.Settings.AddWatcher<int>("godRays", this.OnGodRaysChanged);

            this.mod.Events.PreGodraysRender += OnSetGodrayUniforms;

            RegisterInjectorProperties();
        }

        private void RegisterInjectorProperties()
        {
            var injector = mod.ShaderInjector;

            injector.RegisterFloatProperty("VOLUMETRIC_FLATNESS", () =>
            {
                var volFlatnessInt = ModSettings.VolumetricLightingFlatness;
                return (200 - volFlatnessInt) * 0.01f;
            });

            injector.RegisterFloatProperty("VOLUMETRIC_INTENSITY",
                () => ModSettings.VolumetricLightingIntensity * 0.01f);
        }

        private static void OnShadowMapChanged(int quality)
        {
            if (quality == 0)
            {
                // turn off VL
                ClientSettings.GodRayQuality = 0;
            }
        }

        private void OnGodRaysChanged(int quality)
        {
            if (quality != 1 || ClientSettings.ShadowMapQuality != 0) return;

            // turn on shadow mapping
            ClientSettings.ShadowMapQuality = 1;
            mod.capi.GetClientPlatformAbstract().RebuildFrameBuffers();
        }

        public void OnSetGodrayUniforms(ShaderProgramGodrays rays)
        {
            // custom uniform calls
            var calendar = mod.capi.World.Calendar;
            var dropShadowIntensityObj = typeof(AmbientManager)
                .GetField("DropShadowIntensity", BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(mod.capi.Ambient);

            if (dropShadowIntensityObj == null)
            {
                mod.Mod.Logger.Fatal("DropShadowIntensity not found!");
                return;
            }

            var dropShadowIntensity = (float) dropShadowIntensityObj;

            rays.Uniform("moonLightStrength", calendar.MoonLightStrength);
            rays.Uniform("sunLightStrength", calendar.SunLightStrength);
            rays.Uniform("dayLightStrength", calendar.DayLightStrength);
            rays.Uniform("shadowIntensity", dropShadowIntensity);
            rays.Uniform("flatFogDensity", mod.capi.Ambient.BlendedFlatFogDensity);
        }
    }
}