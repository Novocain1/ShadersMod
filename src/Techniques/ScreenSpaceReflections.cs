using System;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;

namespace Shaders
{
    public class ScreenSpaceReflections : IRenderer
    {
        private readonly ShadersMod mod;
        
        private bool Enabled { get => ModSettings.ScreenSpaceReflectionsEnabled; set => ModSettings.ScreenSpaceReflectionsEnabled = value; }
        
        private FrameBufferRef ssrFramebuffer;
        private FrameBufferRef ssrOutFramebuffer;
        private FrameBufferRef causticsFramebuffer;

        private IShaderProgram[] ssrShaderByRenderPass = new IShaderProgram[Enum.GetValues(typeof(EnumChunkRenderPass)).Length];
        private IShaderProgram ssrOutShader;
        private IShaderProgram causticsShader;
        
        private readonly ClientMain game;
        private readonly ClientPlatformWindows platform;
        private ChunkRenderer chunkRenderer;
        private MeshRef screenQuad;

        private int fbWidth;
        private int fbHeight;

        private float targetWindSpeed;
        private float curWindSpeed;

        int[] waterTextures = new int[4];

        int causticsTexture;
        
        public ScreenSpaceReflections(ShadersMod mod)
        {
            this.mod = mod;

            RegisterInjectorProperties();

            game = mod.capi.GetClient();
            platform = game.GetClientPlatformWindows();
            
            mod.capi.Event.ReloadShader += ReloadShaders;
            ReloadShaders();

            mod.capi.Settings.AddWatcher<bool>("volumetricshading_screenSpaceReflections", OnEnabledChanged);

            mod.capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "ssrWorld");
            mod.capi.Event.RegisterRenderer(this, EnumRenderStage.AfterPostProcessing, "ssrOut");

            mod.Events.RebuildFramebuffers += (a) => SetupFramebuffers();
            SetupFramebuffers();

            /*test = mod.capi.Render.LoadTextureArray(new AssetLocation[]
            {
                new AssetLocation("shadersmod:textures/environment/water/1.png"),
                new AssetLocation("shadersmod:textures/environment/water/2.png"),
                new AssetLocation("shadersmod:textures/environment/water/3.png")
            }, 64, 64);
            */

            waterTextures[0] = mod.capi.Render.GetOrLoadTexture(new AssetLocation("shadersmod:textures/environment/water/1.png"));
            waterTextures[1] = mod.capi.Render.GetOrLoadTexture(new AssetLocation("shadersmod:textures/environment/water/2.png"));
            waterTextures[2] = mod.capi.Render.GetOrLoadTexture(new AssetLocation("shadersmod:textures/environment/water/3.png"));
            waterTextures[3] = mod.capi.Render.GetOrLoadTexture(new AssetLocation("shadersmod:textures/environment/imperfect.png"));

            causticsTexture = mod.capi.Render.GetOrLoadTexture(new AssetLocation("shadersmod:textures/environment/caustics.png"));
        }

        private void RegisterInjectorProperties()
        {
            var injector = mod.ShaderInjector;
            injector.RegisterBoolProperty("VSMOD_SSR", () => ModSettings.ScreenSpaceReflectionsEnabled);

            injector.RegisterBoolProperty("VSMOD_SSR_DIFFRACTION", () => ModSettings.SSRDiffraction);

            injector.RegisterBoolProperty("VSMOD_SSR_BLURREDNM", () => ModSettings.SSRBlurredNormal);

            injector.RegisterFloatProperty("VSMOD_SSR_WATER_TRANSPARENCY",
                () => (100 - ModSettings.SSRWaterTransparency) * 0.01f);

            injector.RegisterFloatProperty("VSMOD_SSR_SPLASH_TRANSPARENCY",
                () => (100 - ModSettings.SSRSplashTransparency) * 0.01f);

            injector.RegisterFloatProperty("VSMOD_SSR_REFLECTION_DIMMING",
                () => ModSettings.SSRReflectionDimming * 0.01f);

            injector.RegisterFloatProperty("VSMOD_SSR_TINT_INFLUENCE",
                () => ModSettings.SSRTintInfluence * 0.01f);

            injector.RegisterFloatProperty("VSMOD_SSR_SKY_MIXIN",
                () => ModSettings.SSRSkyMixin * 0.01f);
        }

        private void OnEnabledChanged(bool enabled)
        {
            this.Enabled = enabled;
        }

        private bool ReloadShaders()
        {
            var success = true;
            ShaderProgram shader;

            for (int i = 0; i < ssrShaderByRenderPass.Length; i++)
            {
                var val = ssrShaderByRenderPass[i];
                val?.Dispose();

                shader = (ShaderProgram)mod.capi.Shader.NewShaderProgram();
                shader.AssetDomain = mod.Mod.Info.ModID;
                mod.capi.Shader.RegisterFileShaderProgram("ssrworld", shader);
                success &= shader.Compile();

                ssrShaderByRenderPass[i] = shader;
            }

            ssrOutShader?.Dispose();

            shader = (ShaderProgram)mod.capi.Shader.NewShaderProgram();
            shader.AssetDomain = mod.Mod.Info.ModID;
            mod.capi.Shader.RegisterFileShaderProgram("ssrout", shader);
            success &= shader.Compile();

            ssrOutShader = shader;

            causticsShader?.Dispose();

            shader = (ShaderProgram)mod.capi.Shader.NewShaderProgram();
            shader.AssetDomain = mod.Mod.Info.ModID;
            mod.capi.Shader.RegisterFileShaderProgram("caustics", shader);
            success &= shader.Compile();

            causticsShader = shader;

            return success;
        }

        public void SetupFramebuffers()
        {
            mod.Mod.Logger.Event("Recreating framebuffers");

            if (ssrFramebuffer != null)
            {
                // dispose the old framebuffer
                platform.DisposeFrameBuffer(ssrFramebuffer);
            }

            if (ssrOutFramebuffer != null)
            {
                platform.DisposeFrameBuffer(ssrOutFramebuffer);
            }

            // create new framebuffer
            fbWidth = (int) (platform.window.Width * ClientSettings.SSAA);
            fbHeight = (int) (platform.window.Height * ClientSettings.SSAA);
            ssrFramebuffer = new FrameBufferRef
            {
                FboId = GL.GenFramebuffer(), Width = fbWidth, Height = fbHeight, 
                DepthTextureId = GL.GenTexture(),
                ColorTextureIds = ArrayUtil.CreateFilled(5, _ => GL.GenTexture())
            };
            
            // bind and setup textures
            ssrFramebuffer.SetupTextures(new[] { 0, 1, 4 }, new[] { 2, 3 });

            // setup output framebuffer
            ssrOutFramebuffer = new FrameBufferRef
            {
                FboId = GL.GenFramebuffer(), Width = fbWidth, Height = fbHeight,
                ColorTextureIds = ArrayUtil.CreateFilled(2, _ => GL.GenTexture())
            };
            
            ssrOutFramebuffer.SetupTextures(new int[] { 1 }, new int[] { 0 }, false);

            causticsFramebuffer = new FrameBufferRef
            {
                FboId = GL.GenFramebuffer(),
                Width = fbWidth,
                Height = fbHeight,
                ColorTextureIds = ArrayUtil.CreateFilled(1, _ => GL.GenTexture())
            };

            causticsFramebuffer.SetupTextures(new int[0], new int[] { 0 }, false);

            screenQuad = platform.GetScreenQuad();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!Enabled) return;
            
            targetWindSpeed = (float)mod.capi.World.BlockAccessor.GetWindSpeedAt(game.EntityPlayer.Pos.XYZ).X;
            curWindSpeed += (targetWindSpeed - curWindSpeed) * 0.001f;

            if (chunkRenderer == null)
            {
                chunkRenderer = game.GetChunkRenderer();
            }

            if (stage == EnumRenderStage.Opaque)
            {
                OnRenderssr();
            } else if (stage == EnumRenderStage.AfterPostProcessing)
            {
                OnRenderSsrOut();
            }
        }

        private void OnRenderSsrOut()
        {
            if (ssrOutFramebuffer == null) return;
            if (ssrOutShader == null) return;

            platform.LoadFrameBuffer(ssrOutFramebuffer);
            GL.ClearBuffer(ClearBuffer.Color, 0, new []{0f, 0f, 0f, 0f});

            var uniforms = mod.capi.Render.ShaderUniforms;
            var ambient = mod.capi.Ambient;
            
            // thanks for declaring this internal btw
            var dayLight = 1.25f * GameMath.Max(mod.capi.World.Calendar.DayLightStrength - mod.capi.World.Calendar.MoonLightStrength / 2f, 0.05f);

            var shader = ssrOutShader;

            shader.Use();
            GL.Enable(EnableCap.Blend);

            var invProjMatrix = new float[16];
            var invModelViewMatrix = new float[16];
            shader.BindTexture2D("primaryScene", platform.FrameBuffers[(int) EnumFrameBuffer.Primary].ColorTextureIds[0], 0);
            shader.BindTexture2D("gPosition", ssrFramebuffer.ColorTextureIds[0], 1);
            shader.BindTexture2D("gNormal", ssrFramebuffer.ColorTextureIds[1], 2);
            shader.BindTexture2D("gDepth", platform.FrameBuffers[(int) EnumFrameBuffer.Primary].DepthTextureId, 3);
            shader.BindTexture2D("gTint", ssrFramebuffer.ColorTextureIds[2], 4);
            shader.BindTexture2D("gLight", ssrFramebuffer.ColorTextureIds[3], 5);
            shader.BindTexture2D("gDiffraction", ssrFramebuffer.ColorTextureIds[4], 6);
            shader.UniformMatrix("projectionMatrix", mod.capi.Render.CurrentProjectionMatrix);
            shader.UniformMatrix("invProjectionMatrix", Mat4f.Invert(invProjMatrix, mod.capi.Render.CurrentProjectionMatrix));
            shader.UniformMatrix("invModelViewMatrix", Mat4f.Invert(invModelViewMatrix, mod.capi.Render.CameraMatrixOriginf));
            shader.Uniform("zNear", uniforms.ZNear);
            shader.Uniform("zFar", uniforms.ZNear);
            shader.Uniform("sunPosition", mod.capi.World.Calendar.SunPositionNormalized);
            shader.Uniform("dayLight", dayLight);
            shader.Uniform("horizonFog", ambient.BlendedCloudDensity);
            shader.Uniform("fogDensityIn", ambient.BlendedFogDensity);
            shader.Uniform("fogMinIn", ambient.BlendedFogMin);
            shader.Uniform("rgbaFog", ambient.BlendedFogColor);

            platform.RenderFullscreenTriangle(screenQuad);
            shader.Stop();

            if (causticsFramebuffer != null && causticsShader != null)
            {
                platform.LoadFrameBuffer(causticsFramebuffer);

                GL.ClearBuffer(ClearBuffer.Color, 0, new[] { 0f, 0f, 0f, 0f });

                shader = causticsShader;
                shader.Use();

                shader.BindTexture2D("gDepth", platform.FrameBuffers[(int)EnumFrameBuffer.Primary].DepthTextureId, 0);
                shader.BindTexture2D("gNormal", ssrFramebuffer.ColorTextureIds[1], 1);
                shader.BindTexture2D("caustics", causticsTexture, 2);
                shader.BindTexture2D("gLight", ssrFramebuffer.ColorTextureIds[3], 3);
                shader.UniformMatrix("invProjectionMatrix", invProjMatrix);
                shader.UniformMatrix("invModelViewMatrix", invModelViewMatrix);
                shader.Uniform("dayLight", dayLight);
                shader.Uniform("playerPos", uniforms.PlayerPos);
                shader.Uniform("sunPosition", uniforms.SunPosition3D);
                shader.Uniform("waterFlowCounter", uniforms.WaterFlowCounter);

                if (ShaderProgramBase.shadowmapQuality > 0)
                {
                    var fbShadowFar = platform.FrameBuffers[(int)EnumFrameBuffer.ShadowmapFar];
                    shader.BindTexture2D("shadowMapFar", fbShadowFar.DepthTextureId, 4);
                    shader.BindTexture2D("shadowMapNear", platform.FrameBuffers[(int)EnumFrameBuffer.ShadowmapNear].DepthTextureId, 5);
                    shader.Uniform("shadowMapWidthInv", 1f / fbShadowFar.Width);
                    shader.Uniform("shadowMapHeightInv", 1f / fbShadowFar.Height);

                    shader.Uniform("shadowRangeFar", uniforms.ShadowRangeFar);
                    shader.Uniform("shadowRangeNear", uniforms.ShadowRangeNear);
                    shader.UniformMatrix("toShadowMapSpaceMatrixFar", uniforms.ToShadowMapSpaceMatrixFar);
                    shader.UniformMatrix("toShadowMapSpaceMatrixNear", uniforms.ToShadowMapSpaceMatrixNear);
                }

                shader.Uniform("fogDensityIn", ambient.BlendedFogDensity);
                shader.Uniform("fogMinIn", ambient.BlendedFogMin);
                shader.Uniform("rgbaFog", ambient.BlendedFogColor);

                platform.RenderFullscreenTriangle(screenQuad);
                shader.Stop();
                platform.CheckGlError("Error while calculating caustics");
            }

            platform.LoadFrameBuffer(EnumFrameBuffer.Primary);
            
            platform.CheckGlError("Error while calculating SSR");
        }
        
        public static void ClearBuffers(int num)
        {
            for (int i = 0; i < num; i++) GL.ClearBuffer(ClearBuffer.Color, i, new[] { 0f, 0f, 0f, 1f });
        }

        public static void BlendBuffers(int num)
        {
            for (int i = 0; i < num; i++) GL.BlendFunc(i, BlendingFactorSrc.OneMinusSrcAlpha, BlendingFactorDest.SrcAlpha);
        }

        private void OnRenderssr()
        {
            if (ssrFramebuffer == null) return;

            var playerWaterDepth = game.playerProperties.EyesInWaterDepth;
            var playerInWater = playerWaterDepth >= 0.1f;
            var playerUnderwater = playerInWater ? 0f : 1f;

            // copy the depth buffer so we can work with it
            var primaryBuffer = platform.FrameBuffers[(int)EnumFrameBuffer.Primary];
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, primaryBuffer.FboId);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, ssrFramebuffer.FboId);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.BlitFramebuffer(0, 0, primaryBuffer.Width, primaryBuffer.Height, 0, 0, fbWidth, fbHeight, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

            // bind our framebuffer
            platform.LoadFrameBuffer(ssrFramebuffer);
            ClearBuffers(7);

            platform.GlEnableCullFace();
            platform.GlDepthMask(false);
            platform.GlEnableDepthTest();
            GL.Enable(EnableCap.Blend);
            BlendBuffers(7);

            // render stuff
            game.GlPushMatrix();
            game.GlLoadMatrix(mod.capi.Render.CameraMatrixOrigin);
            var textureIds = chunkRenderer.GetField<int[]>("textureIds");

            for (int i = 0; i < textureIds.Length; i++)
            {
                for (int j = 0; j < chunkRenderer.poolsByRenderPass.Length; j++)
                {
                    platform.GlEnableCullFace();
                    if (j == (int)EnumChunkRenderPass.BlendNoCull || j == (int)EnumChunkRenderPass.OpaqueNoCull) platform.GlDisableCullFace();

                    var shader = ssrShaderByRenderPass[j];
                    if (shader == null) continue;

                    shader.Use();
                    shader.BindTexture2D("terrainTex", textureIds[i], 0);
                    shader.UniformMatrix("projectionMatrix", mod.capi.Render.CurrentProjectionMatrix);
                    shader.UniformMatrix("modelViewMatrix", mod.capi.Render.CurrentModelviewMatrix);
                    shader.Uniform("dropletIntensity", chunkRenderer.GetField<float>("curRainFall"));
                    shader.Uniform("windIntensity", curWindSpeed);
                    shader.Uniform("playerUnderwater", playerUnderwater);
                    shader.BindTexture2D("water1", waterTextures[0], 1);
                    shader.BindTexture2D("water2", waterTextures[1], 2);
                    shader.BindTexture2D("water3", waterTextures[2], 3);
                    shader.BindTexture2D("imperfect", waterTextures[3], 4);
                    shader.Uniform("rgbaAmbientIn", game.GetField<AmbientManager>("AmbientManager").BlendedAmbientColor);

                    shader.Uniform("renderPass", j);
                    foreach (var pool in chunkRenderer.poolsByRenderPass[j])
                    {
                        pool.Render(game.EntityPlayer.CameraPos, "origin");
                    }
                    
                    shader.Stop();
                }
            }
            
            game.GlPopMatrix();
            platform.UnloadFrameBuffer(ssrFramebuffer);
            
            platform.GlDepthMask(false);
            platform.GlToggleBlend(true);
            
            platform.CheckGlError("Error while rendering solid liquids");
        }

        public void OnSetFinalUniforms(ShaderProgramFinal final)
        {
            if (!Enabled) return;
            if (ssrOutFramebuffer == null) return;
            
            final.BindTexture2D("ssrScene", ssrOutFramebuffer.ColorTextureIds[0]);
            final.BindTexture2D("diffraction", ssrOutFramebuffer.ColorTextureIds[1]);
            final.BindTexture2D("caustics", causticsFramebuffer.ColorTextureIds[0]);
        }

        public void Dispose()
        {
            var windowsPlatform = mod.capi.GetClientPlatformWindows();

            if (ssrFramebuffer != null)
            {
                // dispose the old framebuffer
                windowsPlatform.DisposeFrameBuffer(ssrFramebuffer);
                ssrFramebuffer = null;
            }

            if (ssrOutFramebuffer != null)
            {
                windowsPlatform.DisposeFrameBuffer(ssrOutFramebuffer);
                ssrOutFramebuffer = null;
            }


            for (int i = 0; i < ssrShaderByRenderPass.Length; i++)
            {
                var val = ssrShaderByRenderPass[i];
                val?.Dispose();
                ssrShaderByRenderPass[i] = null;
            }

            if (ssrOutShader != null)
            {
                ssrOutShader.Dispose();
                ssrOutShader = null;
            }

            chunkRenderer = null;
            screenQuad = null;
        }

        public double RenderOrder => 1;

        public int RenderRange => Int32.MaxValue;
    }
}
