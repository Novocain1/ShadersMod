using Vintagestory.API.Client;

namespace Shaders
{
    public class ShadowTweaksGui : AdvancedOptionsDialog
    {
        protected override string DialogKey => "vsmodShadowTweaksConfigure";
        protected override string DialogTitle => "Shadow Tweaks";

        public ShadowTweaksGui(ICoreClientAPI capi) : base(capi)
        {
            RegisterOption(new ConfigOption
            {
                SliderKey = "shadowBaseWidthSlider",
                Text = "Near base width",
                Tooltip = "Sets the base width of the near shadow map. Increases sharpness of near shadows," +
                          "but decreases sharpness of mid-distance ones. Unmodified game value is 30.",
                SlideAction = OnShadowBaseWidthSliderChanged
            });
            
            RegisterOption(new ConfigOption
            {
                SliderKey = "nearPeterPanningSlider",
                Text = "Near offset adjustment",
                Tooltip = "Adjusts the near shadow map Z offset. Reduces peter panning, but might lead to artifacts.",
                SlideAction = OnNearPeterPanningChanged
            });
            
            RegisterOption(new ConfigOption
            {
                SliderKey = "farPeterPanningSlider",
                Text = "Far offset adjustment",
                Tooltip = "Adjusts the far shadow map Z offset. Reduces peter panning, but might lead to artifacts.",
                SlideAction = OnFarPeterPanningChanged
            });
        }

        protected override void RefreshValues()
        {
            SingleComposer.GetSlider("shadowBaseWidthSlider")
                .SetValues(ShadersMod.Settings.NearShadowBaseWidth, 5, 30, 1);
            
            SingleComposer.GetSlider("nearPeterPanningSlider")
                .SetValues(ShadersMod.Settings.NearPeterPanningAdjustment, 0, 4, 1);
            
            SingleComposer.GetSlider("farPeterPanningSlider")
                .SetValues(ShadersMod.Settings.FarPeterPanningAdjustment, 0, 8, 1);
            base.RefreshValues();
        }

        private bool OnShadowBaseWidthSliderChanged(int value)
        {
            ShadersMod.Settings.NearShadowBaseWidth = value;
            return true;
        }

        private bool OnNearPeterPanningChanged(int value)
        {
            ShadersMod.Settings.NearPeterPanningAdjustment = value;
            return true;
        }
        
        private bool OnFarPeterPanningChanged(int value)
        {
            ShadersMod.Settings.FarPeterPanningAdjustment = value;
            return true;
        }
    }
}