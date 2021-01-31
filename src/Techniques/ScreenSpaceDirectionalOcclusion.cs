namespace Shaders
{
    public class ScreenSpaceDirectionalOcclusion
    {
        public ScreenSpaceDirectionalOcclusion(ShadersMod mod)
        {
            var injector = mod.ShaderInjector;
            injector.RegisterBoolProperty("SSDO", () => ModSettings.SSDOEnabled);
        }
    }
}