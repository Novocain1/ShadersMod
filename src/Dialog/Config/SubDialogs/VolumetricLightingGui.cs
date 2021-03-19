using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace Shaders
{
    class VolumetricLightingGui : AdvancedOptionsDialog
    {
        protected override string DialogKey => "vsmodVolumetricLightingConfigure";
        protected override string DialogTitle => "Volumetric Lighting Options";

        public VolumetricLightingGui(ICoreClientAPI capi) : base(capi)
        {
            RegisterOption(new ConfigOption
            {
                SwitchKey = "toggleVolumetricLighting",
                Text = "Enable Volumetric Lighting",
                ToggleAction = ToggleVolumetricLighting
            });
            
            RegisterOption(new ConfigOption
            {
                SliderKey = "intensitySlider",
                Text = "Intensity",
                Tooltip = "The intensity of the Volumetric Lighting effect",
                SlideAction = OnIntensitySliderChanged
            });
            
            RegisterOption(new ConfigOption
            {
                SliderKey = "flatnessSlider",
                Text = "Flatness",
                Tooltip = "Defines how noticeable the difference between low and high scattering is",
                SlideAction = OnFlatnessSliderChanged
            });
        }

        protected override void RefreshValues()
        {
            if (!IsOpened()) return;

            SingleComposer.GetSwitch("toggleVolumetricLighting").On = ClientSettings.GodRayQuality > 0;
            SingleComposer.GetSlider("flatnessSlider").SetValues(
                ShadersMod.Settings.VolumetricLightingFlatness, 1, 199, 1);
            SingleComposer.GetSlider("intensitySlider").SetValues(
                ShadersMod.Settings.VolumetricLightingIntensity, 1, 100, 1);
            base.RefreshValues();
        }

        private void ToggleVolumetricLighting(bool on)
        {
            ClientSettings.GodRayQuality = on ? 1 : 0;
            if (on && ClientSettings.ShadowMapQuality == 0)
            {
                // need shadowmapping
                ClientSettings.ShadowMapQuality = 1;
            }
            RefreshValues();
        }

        private bool OnFlatnessSliderChanged(int value)
        {
            ShadersMod.Settings.VolumetricLightingFlatness = value;
            RefreshValues();
            return true;
        }

        private bool OnIntensitySliderChanged(int value)
        {
            ShadersMod.Settings.VolumetricLightingIntensity = value;
            RefreshValues();
            return true;
        }
    }
}