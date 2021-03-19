using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace Shaders
{
    public class ModSettingsBase
    {
        public virtual bool ScreenSpaceReflectionsEnabled { get; set; }
        public virtual bool SSRBlurredNormal { get; set; }
        public virtual int VolumetricLightingFlatness { get; set; }
        public virtual int VolumetricLightingIntensity { get; set; }
        public virtual bool SSDOEnabled { get; set; }
        public virtual bool SSRDiffraction { get; set; }
        public virtual int SSRWaterTransparency { get; set; }
        public virtual int SSRSplashTransparency { get; set; }
        public virtual int SSRReflectionDimming { get; set; }
        public virtual int SSRTintInfluence { get; set; }
        public virtual int SSRSkyMixin { get; set; }
        public virtual int OverexposureIntensity { get; set; }
        public virtual int SunBloomIntensity { get; set; }
        public virtual int NearShadowBaseWidth { get; set; }
        public virtual int NearPeterPanningAdjustment { get; set; }
        public virtual int FarPeterPanningAdjustment { get; set; }
    }

    public class ModSettings : ModSettingsBase
    {
        ICoreClientAPI capi;

        public const string DefaultPreset =
        @"
        {
          ""ScreenSpaceReflectionsEnabled"": true,
          ""SSRBlurredNormal"": false,
          ""VolumetricLightingFlatness"": 76,
          ""VolumetricLightingIntensity"": 33,
          ""SSDOEnabled"": true,
          ""SSRDiffraction"": true,
          ""SSRWaterTransparency"": 0,
          ""SSRSplashTransparency"": 65,
          ""SSRReflectionDimming"": 105,
          ""SSRTintInfluence"": 70,
          ""SSRSkyMixin"": 22,
          ""OverexposureIntensity"": 31,
          ""SunBloomIntensity"": 49,
          ""NearShadowBaseWidth"": 15,
          ""NearPeterPanningAdjustment"": 2,
          ""FarPeterPanningAdjustment"": 5
        }";

        public ModSettings(ICoreClientAPI capi)
        {
            this.capi = capi;
            Load();
        }

        public void ResetToDefault()
        {
            var load = JsonConvert.DeserializeObject<ModSettingsBase>(DefaultPreset);
            Parse(load);
            Store();
        }

        public void Store()
        {
            capi.StoreModConfig(this as ModSettingsBase, "ShadersMod.json");
        }

        public void Parse(ModSettingsBase load)
        {
            this.FarPeterPanningAdjustment = load.FarPeterPanningAdjustment;
            this.NearPeterPanningAdjustment = load.NearPeterPanningAdjustment;
            this.NearShadowBaseWidth = load.NearShadowBaseWidth;
            this.OverexposureIntensity = load.OverexposureIntensity;
            this.ScreenSpaceReflectionsEnabled = load.ScreenSpaceReflectionsEnabled;
            this.SSDOEnabled = load.SSDOEnabled;
            this.SSRBlurredNormal = load.SSRBlurredNormal;
            this.SSRDiffraction = load.SSRDiffraction;
            this.SSRReflectionDimming = load.SSRReflectionDimming;
            this.SSRSkyMixin = load.SSRSkyMixin;
            this.SSRSplashTransparency = load.SSRSplashTransparency;
            this.SSRTintInfluence = load.SSRTintInfluence;
            this.SSRWaterTransparency = load.SSRWaterTransparency;
            this.SunBloomIntensity = load.SunBloomIntensity;
            this.VolumetricLightingFlatness = load.VolumetricLightingFlatness;
            this.VolumetricLightingIntensity = load.VolumetricLightingIntensity;
        }

        public void Load()
        {
            var load = capi.LoadModConfig<ModSettingsBase>("ShadersMod.json") ?? JsonConvert.DeserializeObject<ModSettingsBase>(DefaultPreset);
            Parse(load);
            Store();
        }

        public override bool ScreenSpaceReflectionsEnabled
        {
            get => ClientSettings.Inst.GetBoolSetting("volumetricshading_screenSpaceReflections");
            set { ClientSettings.Inst.Bool["volumetricshading_screenSpaceReflections"] = value; Store(); }
        }

        public override bool SSRBlurredNormal
        {
            get => ClientSettings.Inst.GetBoolSetting("volumetricshading_SSRBlurredNormal");
            set => ClientSettings.Inst.Bool["volumetricshading_SSRBlurredNormal"] = value;
        }

        public override int VolumetricLightingFlatness
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_volumetricLightingFlatness");
            set { ClientSettings.Inst.Int["volumetricshading_volumetricLightingFlatness"] = value; Store(); }
        }

        public override int VolumetricLightingIntensity
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_volumetricLightingIntensity");
            set { ClientSettings.Inst.Int["volumetricshading_volumetricLightingIntensity"] = value; Store(); }
        }

        public override bool SSDOEnabled
        {
            get => ClientSettings.Inst.GetBoolSetting("volumetricshading_SSDO");
            set { ClientSettings.Inst.Bool["volumetricshading_SSDO"] = value; Store(); }
        }

        public override bool SSRDiffraction
        {
            get => ClientSettings.Inst.GetBoolSetting("volumetricshading_SSRDiffraction");
            set { ClientSettings.Inst.Bool["volumetricshading_SSRDiffraction"] = value; Store(); }
        }

        public override int SSRWaterTransparency
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_SSRWaterTransparency");
            set { ClientSettings.Inst.Int["volumetricshading_SSRWaterTransparency"] = value; Store(); }
        }

        public override int SSRSplashTransparency
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_SSRSplashTransparency");
            set { ClientSettings.Inst.Int["volumetricshading_SSRSplashTransparency"] = value; Store(); }
        }

        public override int SSRReflectionDimming
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_SSRReflectionDimming");
            set { ClientSettings.Inst.Int["volumetricshading_SSRReflectionDimming"] = value; Store(); }
        }

        public override int SSRTintInfluence
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_SSRTintInfluence");
            set { ClientSettings.Inst.Int["volumetricshading_SSRTintInfluence"] = value; Store(); }
        }

        public override int SSRSkyMixin
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_SSRSkyMixin");
            set { ClientSettings.Inst.Int["volumetricshading_SSRSkyMixin"] = value; Store(); }
        }

        public override int OverexposureIntensity
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_overexposureIntensity");
            set { ClientSettings.Inst.Int["volumetricshading_overexposureIntensity"] = value; Store(); }
        }

        public override int SunBloomIntensity
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_sunBloomIntensity");
            set { ClientSettings.Inst.Int["volumetricshading_sunBloomIntensity"] = value; Store(); }
        }

        public override int NearShadowBaseWidth
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_nearShadowBaseWidth");
            set { ClientSettings.Inst.Int["volumetricshading_nearShadowBaseWidth"] = value; Store(); }
        }

        public override int NearPeterPanningAdjustment
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_nearPeterPanningAdjustment");
            set { ClientSettings.Inst.Int["volumetricshading_nearPeterPanningAdjustment"] = value; Store(); }
        }
        
        public override int FarPeterPanningAdjustment
        {
            get => ClientSettings.Inst.GetIntSetting("volumetricshading_farPeterPanningAdjustment");
            set { ClientSettings.Inst.Int["volumetricshading_farPeterPanningAdjustment"] = value; Store(); }
        }
    }
}