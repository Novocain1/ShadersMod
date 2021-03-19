using Vintagestory.API.Client;

namespace Shaders
{
    class ScreenSpaceReflectionsGui : AdvancedOptionsDialog
    {
        protected override string DialogKey => "vsmodSSRConfigure";
        protected override string DialogTitle => "Screen Space Reflections Options";

        public ScreenSpaceReflectionsGui(ICoreClientAPI capi) : base(capi)
        {
            RegisterOption(new ConfigOption
            {
                SwitchKey = "toggleSSR",
                Text = "Enable Screen Space Reflections",
                ToggleAction = ToggleSSR
            });

            RegisterOption(new ConfigOption
            {
                SwitchKey = "toggleDiffraction",
                Text = "Enable Diffraction",
                ToggleAction = ToggleDiffraction
            });

            RegisterOption(new ConfigOption
            {
                SliderKey = "dimmingSlider",
                Text = "Reflection dimming",
                Tooltip = "The dimming effect strength on the reflected image",
                SlideAction = OnDimmingSliderChanged
            });
            
            RegisterOption(new ConfigOption
            {
                SliderKey = "transparencySlider",
                Text = "Water transparency",
                Tooltip = "Sets the transparency of the vanilla water effect",
                SlideAction = OnTransparencySliderChanged
            });
            
            RegisterOption(new ConfigOption
            {
                SliderKey = "splashTransparencySlider",
                Text = "Splash transparency",
                Tooltip = "The strength of the vanilla splash effect",
                SlideAction = OnSplashTransparencySliderChanged
            });
            
            RegisterOption(new ConfigOption
            {
                SliderKey = "tintSlider",
                Text = "Tint influence",
                Tooltip = "Sets the influence an object's tint has on it's reflection color",
                SlideAction = OnTintSliderChanged
            });
            
            RegisterOption(new ConfigOption
            {
                SliderKey = "skyMixinSlider",
                Text = "Sky mixin",
                Tooltip = "The amount of sky color that is always visible, even when fully reflecting",
                SlideAction = OnSkyMixinSliderChanged
            });
        }

        protected override void RefreshValues()
        {
            SingleComposer.GetSwitch("toggleSSR").SetValue(ShadersMod.Settings.ScreenSpaceReflectionsEnabled);
            SingleComposer.GetSwitch("toggleDiffraction").SetValue(ShadersMod.Settings.SSRDiffraction);
            SingleComposer.GetSlider("dimmingSlider").SetValues(ShadersMod.Settings.SSRReflectionDimming, 1, 400, 1);
            SingleComposer.GetSlider("transparencySlider").SetValues(ShadersMod.Settings.SSRWaterTransparency, 0, 100, 1);
            SingleComposer.GetSlider("tintSlider").SetValues(ShadersMod.Settings.SSRTintInfluence, 0, 100, 1);
            SingleComposer.GetSlider("skyMixinSlider").SetValues(ShadersMod.Settings.SSRSkyMixin, 0, 100, 1);
            SingleComposer.GetSlider("splashTransparencySlider")
                .SetValues(ShadersMod.Settings.SSRSplashTransparency, 0, 100, 1);
            base.RefreshValues();
        }

        private void ToggleSSR(bool on)
        {
            ShadersMod.Settings.ScreenSpaceReflectionsEnabled = on;
            RefreshValues();
        }

        private void ToggleDiffraction(bool on)
        {
            ShadersMod.Settings.SSRDiffraction = on;
            RefreshValues();
        }

        private bool OnDimmingSliderChanged(int value)
        {
            ShadersMod.Settings.SSRReflectionDimming = value;
            RefreshValues();
            return true;
        }

        private bool OnTransparencySliderChanged(int value)
        {
            ShadersMod.Settings.SSRWaterTransparency = value;
            RefreshValues();
            return true;
        }

        private bool OnSplashTransparencySliderChanged(int value)
        {
            ShadersMod.Settings.SSRSplashTransparency = value;
            RefreshValues();
            return true;
        }

        private bool OnTintSliderChanged(int value)
        {
            ShadersMod.Settings.SSRTintInfluence = value;
            RefreshValues();
            return true;
        }

        private bool OnSkyMixinSliderChanged(int value)
        {
            ShadersMod.Settings.SSRSkyMixin = value;
            RefreshValues();
            return true;
        }
    }
}