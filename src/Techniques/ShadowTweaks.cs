namespace Shaders
{
    public class ShadowTweaks
    {
        private readonly ShadersMod _mod;
        public int NearShadowBaseWidth { get; private set; }

        public ShadowTweaks(ShadersMod mod)
        {
            _mod = mod;

            _mod.capi.Settings.AddWatcher<int>("volumetricshading_nearShadowBaseWidth", OnNearShadowBaseWidthChanged);
            NearShadowBaseWidth = ModSettings.NearShadowBaseWidth;
            
            _mod.ShaderInjector.RegisterFloatProperty("VSMOD_NEARSHADOWOFFSET",
                () => ModSettings.NearPeterPanningAdjustment);
            
            _mod.ShaderInjector.RegisterFloatProperty("VSMOD_FARSHADOWOFFSET",
                () => ModSettings.FarPeterPanningAdjustment);
        }
        
        private void OnNearShadowBaseWidthChanged(int newVal)
        {
            NearShadowBaseWidth = newVal;
        }
    }
}