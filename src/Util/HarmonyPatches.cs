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

namespace Shaders
{
    [HarmonyPatch]
    internal class SkyVisibility
    {
        const int chunksize = 32;
        public static bool initialized = false;
       
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

        public static Dictionary<int, bool> reflectiveById = new Dictionary<int, bool>();

        public static bool ReflectiveTests(Block block)
        {
            bool reflective = false;
            reflective |= block.BlockMaterial == EnumBlockMaterial.Ice;
            reflective |= block.BlockMaterial == EnumBlockMaterial.Glass;
            reflective |= block.BlockMaterial == EnumBlockMaterial.Metal;
            reflective |= block.FirstCodePart() == "rockpolished";

            return reflective;
        }

        public static void Initialize(ICoreClientAPI capi)
        {
            foreach (var val in capi.World.Blocks)
            {
                reflectiveById[val.Id] = ReflectiveTests(val);
            }
            initialized = true;
        }

        public static void Tesselate(ref TCTCache vars)
        {
            int flags = vars.drawFaceFlags;
            if (!initialized) Initialize(vars.tct.GetField<ClientMain>("game").Api as ICoreClientAPI);

            vars.VertexFlags.Reflective |= reflectiveById[vars.blockId];

            if ((TileSideFlagsEnum.Up & flags) != 0)
            {
                if (vars.rainHeightMap[(vars.posZ % chunksize) * chunksize + (vars.posX % chunksize)] <= vars.posY)
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
            ShadersMod.Instance.VolumetricLighting.OnSetGodrayUniforms(rays);
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
            ShadersMod.Instance.ScreenSpaceReflections.OnSetFinalUniforms(final);
        }

        [HarmonyPatch("SetupDefaultFrameBuffers")]
        [HarmonyPostfix]
        // ReSharper disable once InconsistentNaming
        public static void SetupDefaultFrameBuffersPostfix(List<FrameBufferRef> __result)
        {
            ShadersMod.Instance.Events.EmitRebuildFramebuffers(__result);
        }
    }

    [HarmonyPatch(typeof(ShaderRegistry))]
    internal class ShaderRegistryPatches
    {
        [HarmonyPatch("LoadShader")]
        [HarmonyPostfix]
        public static void LoadShaderPostfix(ShaderProgram program, EnumShaderType shaderType)
        {
            ShadersMod.Instance.ShaderInjector.OnShaderLoaded(program, shaderType);
        }
    }

    [HarmonyPatch(typeof(ShaderProgram))]
    internal class FixSampler2DArray
    {
        const string uniformTypesRegex = @"(\s|\r\n)uniform\s*(?<type>float|int|ivec2|ivec3|ivec4|vec2|vec3|vec4|sampler2DArray|sampler2DShadow|sampler2D|samplerCube|mat3|mat4)\s*(\[[\d\w]+\])?\s*(?<var>[\d\w]+)";

        [HarmonyPatch("collectUniformNames")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CollectUniformNames(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var val in instructions)
            {
                if (!found && val.opcode == OpCodes.Ldstr)
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Ldstr, uniformTypesRegex);
                }
                else yield return val;
            }
        }
    }

    [HarmonyPatch(typeof(SystemRenderShadowMap))]
    internal class SystemRenderShadowMapPatches
    {
        private static readonly MethodInfo OnRenderShadowNearBaseWidthCallsiteMethod =
            typeof(SystemRenderShadowMapPatches).GetMethod("OnRenderShadowNearBaseWidthCallsite");

        [HarmonyPatch("OnRenderShadowNear")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OnRenderShadowNearBaseWidthTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var first = true;
            foreach (var instruction in instructions)
            {
                if (first)
                {
                    first = false;
                    // replace constant offset
                    yield return new CodeInstruction(OpCodes.Call, OnRenderShadowNearBaseWidthCallsiteMethod);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static int OnRenderShadowNearBaseWidthCallsite()
        {
            return ShadersMod.Instance.ShadowTweaks.NearShadowBaseWidth;
        }

        private static readonly MethodInfo PrepareForShadowRenderingMethod = typeof(SystemRenderShadowMap)
            .GetMethod("PrepareForShadowRendering", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPatch("OnRenderShadowNear")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OnRenderShadowNearZExtend(IEnumerable<CodeInstruction> instructions)
        {
            var found = false;
            CodeInstruction previousInstruction = null;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(PrepareForShadowRenderingMethod))
                {
                    found = true;

                    // fixes some shadow glitches by increasing the extra culling range for shadows
                    yield return new CodeInstruction(OpCodes.Ldc_R4, (float)32);
                }
                else if (previousInstruction != null)
                {
                    yield return previousInstruction;
                }

                previousInstruction = instruction;
            }

            yield return previousInstruction;

            if (!found)
            {
                throw new Exception("Could not patch OnRenderShadowNear for further Z extension");
            }
        }
    }

    [HarmonyPatch(typeof(SystemRenderSunMoon))]
    internal class SunMoonPatches
    {
        private static readonly MethodInfo StandardShaderTextureSetter = typeof(ShaderProgramStandard)
            .GetProperty("Tex2D")?.SetMethod;

        private static readonly MethodInfo AddRenderFlagsSetter = typeof(ShaderProgramStandard)
            .GetProperty("AddRenderFlags")?.SetMethod;

        private static readonly MethodInfo RenderCallsiteMethod = typeof(SunMoonPatches)
            .GetMethod("RenderCallsite");

        [HarmonyPatch("OnRenderFrame3D")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RenderTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var found = false;
            foreach (var instruction in instructions)
            {
                yield return instruction;

                if (!instruction.Calls(StandardShaderTextureSetter)) continue;

                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Call, RenderCallsiteMethod);
                found = true;
            }

            if (found is false)
            {
                throw new Exception("Could not patch RenderPostprocessingEffects!");
            }
        }

        [HarmonyPatch("OnRenderFrame3DPost")]
        [HarmonyPostfix]
        public static void RenderPostPostfix()
        {
            ShadersMod.Instance.OverexposureEffect.OnRenderedSun();
        }

        [HarmonyPatch("OnRenderFrame3D")]
        [HarmonyPostfix]
        public static void RenderPostfix()
        {
            ShadersMod.Instance.OverexposureEffect.OnRenderedSun();
        }

        [HarmonyPatch("OnRenderFrame3DPost")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RenderPostTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var found = false;
            var previousInstructions = new CodeInstruction[2];
            foreach (var instruction in instructions)
            {
                var currentOld = previousInstructions[1];
                yield return instruction;

                previousInstructions[1] = previousInstructions[0];
                previousInstructions[0] = instruction;
                if (!instruction.Calls(AddRenderFlagsSetter)) continue;

                // currentOld contains the code to load our shader program to the stack
                yield return currentOld;
                yield return new CodeInstruction(OpCodes.Call, RenderCallsiteMethod);
                found = true;
            }

            if (found is false)
            {
                throw new Exception("Could not patch RenderFinalComposition!");
            }
        }

        public static void RenderCallsite(ShaderProgramStandard standard)
        {
            ShadersMod.Instance.Events.EmitPreSunRender(standard);
        }
    }
}
