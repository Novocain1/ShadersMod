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
            NearShadowBaseWidth = ShadersMod.Settings.NearShadowBaseWidth;
            
            _mod.ShaderInjector.RegisterFloatProperty("VSMOD_NEARSHADOWOFFSET",
                () => ShadersMod.Settings.NearPeterPanningAdjustment);
            
            _mod.ShaderInjector.RegisterFloatProperty("VSMOD_FARSHADOWOFFSET",
                () => ShadersMod.Settings.FarPeterPanningAdjustment);
        }
        
        private void OnNearShadowBaseWidthChanged(int newVal)
        {
            NearShadowBaseWidth = newVal;
        }
    }
}