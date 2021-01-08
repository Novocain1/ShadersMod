using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;

namespace VolumetricShading
{
    [HarmonyPatch]
    internal class SkyVisibility
    {
        const int chunksize = 32;
       
        #region patch redirectors
        [HarmonyPatch(typeof(CubeTesselator), "Tesselate")][HarmonyPrefix]
        public static void CubeTesselator(ref TCTCache vars) => Tesselate(ref vars);

        [HarmonyPatch(typeof(LiquidTesselator), "Tesselate")][HarmonyPrefix]
        public static void LiquidTesselator(ref TCTCache vars) => Tesselate(ref vars);

        [HarmonyPatch(typeof(TopsoilTesselator), "Tesselate")][HarmonyPrefix]
        public static void TopsoilTesselator(ref TCTCache vars) => Tesselate(ref vars);

        [HarmonyPatch(typeof(JsonTesselator), "Tesselate")][HarmonyPrefix]
        public static void JsonTesselator(ref TCTCache vars) => Tesselate(ref vars);

        [HarmonyPatch(typeof(JsonAndSnowLayerTesselator), "Tesselate")][HarmonyPrefix]
        public static void JsonAndSnowLayerTesselator(ref TCTCache vars) => Tesselate(ref vars);

        [HarmonyPatch(typeof(JsonAndLiquidTesselator), "Tesselate")][HarmonyPrefix]
        public static void JsonAndLiquidTesselator(ref TCTCache vars) => Tesselate(ref vars);
        #endregion

        public static void Tesselate(ref TCTCache vars)
        {
            int flags = vars.drawFaceFlags;

            if (vars.block.BlockMaterial == EnumBlockMaterial.Ice || vars.block.BlockMaterial == EnumBlockMaterial.Glass)
            {
                vars.VertexFlags.Reflective = true;
            }

            if ((TileSideFlagsEnum.Up & flags) != 0)
            {
                if (vars.mapchunk.RainHeightMap[(vars.posZ % chunksize) * chunksize + (vars.posX % chunksize)] <= vars.posY)
                {
                    vars.ColorMapData.Value |= 1 << 13;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ClientPlatformWindows))]
    internal class PlatformPatches
    {
        private static readonly MethodInfo PlayerViewVectorSetter =
            typeof(ShaderProgramGodrays).GetProperty("PlayerViewVector")?.SetMethod;

        private static readonly MethodInfo GodrayCallsiteMethod = typeof(PlatformPatches).GetMethod("GodrayCallsite");
        
        [HarmonyPatch("RenderPostprocessingEffects")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PostprocessingTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var found = false;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                
                if (!instruction.Calls(PlayerViewVectorSetter)) continue;

                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Call, GodrayCallsiteMethod);
                found = true;
            }

            if (found is false)
            {
                throw new Exception("Could not patch RenderPostprocessingEffects!");
            }
        }

        public static void GodrayCallsite(ShaderProgramGodrays rays)
        {
            VolumetricShadingMod.Instance.VolumetricLighting.OnSetGodrayUniforms(rays);
        }
        
        
        
        private static readonly MethodInfo PrimaryScene2DSetter =
            typeof(ShaderProgramFinal).GetProperty("PrimaryScene2D")?.SetMethod;

        private static readonly MethodInfo FinalCallsiteMethod = typeof(PlatformPatches).GetMethod("FinalCallsite");
        
        [HarmonyPatch("RenderFinalComposition")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FinalTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var found = false;
            var previousInstructions = new CodeInstruction[2];
            foreach (var instruction in instructions)
            {
                var currentOld = previousInstructions[1];
                yield return instruction;

                previousInstructions[1] = previousInstructions[0];
                previousInstructions[0] = instruction;
                if (!instruction.Calls(PrimaryScene2DSetter)) continue;

                // currentOld contains the code to load our shader program to the stack
                yield return currentOld;
                yield return new CodeInstruction(OpCodes.Call, FinalCallsiteMethod);
                found = true;
            }

            if (found is false)
            {
                throw new Exception("Could not patch RenderFinalComposition!");
            }
        }

        public static void FinalCallsite(ShaderProgramFinal final)
        {
            VolumetricShadingMod.Instance.ScreenSpaceReflections.OnSetFinalUniforms(final);
        }
        
        
        
        
        [HarmonyPatch("SetupDefaultFrameBuffers")]
        [HarmonyPostfix]
        // ReSharper disable once InconsistentNaming
        public static void SetupDefaultFrameBuffersPostfix(List<FrameBufferRef> __result)
        {
            VolumetricShadingMod.Instance.ScreenSpaceReflections.SetupFramebuffers(__result);
        }
    }

    [HarmonyPatch(typeof(ShaderRegistry))]
    internal class ShaderRegistryPatches
    {
        [HarmonyPatch("LoadShader")]
        [HarmonyPostfix]
        public static void LoadShaderPostfix(ShaderProgram program, EnumShaderType shaderType)
        {
            VolumetricShadingMod.Instance.ShaderInjector.OnShaderLoaded(program, shaderType);
        }
    }
}
