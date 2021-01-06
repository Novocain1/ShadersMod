using System;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;

namespace VolumetricShading
{
    public class ScreenSpaceReflections : IRenderer
    {
        private readonly VolumetricShadingMod _mod;
        
        private bool _enabled;
        
        private FrameBufferRef _ssrFramebuffer;
        private FrameBufferRef _ssrOutFramebuffer;
        private IShaderProgram _ssrShader;
        private IShaderProgram _ssrOutShader;
        
        private readonly ClientMain _game;
        private readonly ClientPlatformWindows _platform;
        private ChunkRenderer _chunkRenderer;
        private MeshRef _screenQuad;

        private int _fbWidth;
        private int _fbHeight;

        private float targetWindSpeed;
        private float curWindSpeed;

        int[] waterTextures = new int[3];
        
        public ScreenSpaceReflections(VolumetricShadingMod mod)
        {
            _mod = mod;

            _game = mod.CApi.GetClient();
            _platform = _game.GetClientPlatformWindows();
            
            mod.CApi.Event.ReloadShader += ReloadShaders;
            ReloadShaders();

            _enabled = ModSettings.ScreenSpaceReflectionsEnabled;
            mod.CApi.Settings.AddWatcher<bool>("volumetricshading_screenSpaceReflections", OnEnabledChanged);

            mod.CApi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "ssr");
            mod.CApi.Event.RegisterRenderer(this, EnumRenderStage.AfterPostProcessing, "ssrOut");
            
            SetupFramebuffers(_platform.FrameBuffers);

            waterTextures[0] = mod.CApi.Render.GetOrLoadTexture(new AssetLocation("volumetricshading:textures/environment/water/1.png"));
            waterTextures[1] = mod.CApi.Render.GetOrLoadTexture(new AssetLocation("volumetricshading:textures/environment/water/2.png"));
            waterTextures[2] = mod.CApi.Render.GetOrLoadTexture(new AssetLocation("volumetricshading:textures/environment/water/3.png"));
        }

        private void OnEnabledChanged(bool enabled)
        {
            _enabled = enabled;
        }

        private bool ReloadShaders()
        {
            var success = true;
            
            _ssrShader?.Dispose();
            _ssrOutShader?.Dispose();

            var shader = (ShaderProgram) _mod.CApi.Shader.NewShaderProgram();
            shader.AssetDomain = _mod.Mod.Info.ModID;
            _mod.CApi.Shader.RegisterFileShaderProgram("ssr", shader);
            if (!shader.Compile()) success = false;
            _ssrShader = shader;

            shader = (ShaderProgram) _mod.CApi.Shader.NewShaderProgram();
            shader.AssetDomain = _mod.Mod.Info.ModID;
            _mod.CApi.Shader.RegisterFileShaderProgram("ssrout", shader);
            if (!shader.Compile()) success = false;
            _ssrOutShader = shader;

            return success;
        }

        private void SetupVertexTexture(FrameBufferRef fbRef, int textureId)
        {
            GL.BindTexture(TextureTarget.Texture2D, fbRef.ColorTextureIds[textureId]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _fbWidth, _fbHeight, 0,
                PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor,
                new[] { 1f, 1f, 1f, 1f });
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int)TextureWrapMode.ClampToBorder);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + textureId,
                TextureTarget.Texture2D, fbRef.ColorTextureIds[textureId], 0);
        }

        private void SetupColorTexture(FrameBufferRef fbRef, int textureId)
        {
            GL.BindTexture(TextureTarget.Texture2D, fbRef.ColorTextureIds[textureId]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _fbWidth, _fbHeight, 0, PixelFormat.Rgba, PixelType.UnsignedShort, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + textureId,
                TextureTarget.Texture2D, fbRef.ColorTextureIds[textureId], 0);
        }

        public void SetupFramebuffers(List<FrameBufferRef> mainBuffers)
        {
            _mod.Mod.Logger.Event("Recreating framebuffers");

            if (_ssrFramebuffer != null)
            {
                // dispose the old framebuffer
                _platform.DisposeFrameBuffer(_ssrFramebuffer);
            }

            if (_ssrOutFramebuffer != null)
            {
                _platform.DisposeFrameBuffer(_ssrOutFramebuffer);
            }

            // create new framebuffer
            _fbWidth = (int) (_platform.window.Width * ClientSettings.SSAA);
            _fbHeight = (int) (_platform.window.Height * ClientSettings.SSAA);
            _ssrFramebuffer = new FrameBufferRef
            {
                FboId = GL.GenFramebuffer(), Width = _fbWidth, Height = _fbHeight
            };
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _ssrFramebuffer.FboId);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D,
                mainBuffers[(int) EnumFrameBuffer.Primary].DepthTextureId, 0);

            // create our normal and position textures
            _ssrFramebuffer.ColorTextureIds = ArrayUtil.CreateFilled(3, _ => GL.GenTexture());

            // bind and setup textures
            for (var i = 0; i < 2; ++i)
            {
                SetupVertexTexture(_ssrFramebuffer, i);
            }
            SetupColorTexture(_ssrFramebuffer, 2);

            GL.DrawBuffers(3, new []{DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2 });

            CheckFbStatus();

            // setup output framebuffer
            _ssrOutFramebuffer = new FrameBufferRef
            {
                FboId = GL.GenFramebuffer(), Width = _fbWidth, Height = _fbHeight
            };
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _ssrOutFramebuffer.FboId);
            _ssrOutFramebuffer.ColorTextureIds = new[] {GL.GenTexture(), GL.GenTexture() };
            for (int i = 0; i < _ssrOutFramebuffer.ColorTextureIds.Length; i++)
            {
                GL.BindTexture(TextureTarget.Texture2D, _ssrOutFramebuffer.ColorTextureIds[i]);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _fbWidth, _fbHeight, 0, PixelFormat.Rgba, PixelType.UnsignedShort, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)TextureMagFilter.Linear);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + i, TextureTarget.Texture2D, _ssrOutFramebuffer.ColorTextureIds[i], 0);
            }

            GL.DrawBuffers(2, new[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1});

            CheckFbStatus();

            _screenQuad = _platform.GetScreenQuad();
        }

        private static void CheckFbStatus()
        {
            var errorCode = GL.Ext.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (errorCode != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("Could not create framebuffer: " + errorCode);
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!_enabled) return;
            
            targetWindSpeed = (float)_mod.CApi.World.BlockAccessor.GetWindSpeedAt(_game.EntityPlayer.Pos.XYZ).X;
            curWindSpeed += (targetWindSpeed - curWindSpeed) * 0.01f;

            if (_chunkRenderer == null)
            {
                _chunkRenderer = _game.GetChunkRenderer();
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
            if (_ssrOutFramebuffer == null) return;
            if (_ssrOutShader == null) return;

            _platform.LoadFrameBuffer(_ssrOutFramebuffer);
            GL.ClearBuffer(ClearBuffer.Color, 0, new []{0f, 0f, 0f, 0f});

            var uniforms = _mod.CApi.Render.ShaderUniforms;
            var ambient = _mod.CApi.Ambient;
            
            // thanks for declaring this internal btw
            var dayLight = 1.25f * GameMath.Max(_mod.CApi.World.Calendar.DayLightStrength - _mod.CApi.World.Calendar.MoonLightStrength / 2f, 0.05f);

            var shader = _ssrOutShader;
            shader.Use();
            GL.Enable(EnableCap.Blend);

            var invProjMatrix = new float[16];
            var invModelViewMatrix = new float[16];
            shader.BindTexture2D("primaryScene", _platform.FrameBuffers[(int) EnumFrameBuffer.Primary].ColorTextureIds[0], 0);
            shader.BindTexture2D("gPosition", _ssrFramebuffer.ColorTextureIds[0], 1);
            shader.BindTexture2D("gNormal", _ssrFramebuffer.ColorTextureIds[1], 2);
            shader.BindTexture2D("gDepth", _platform.FrameBuffers[(int) EnumFrameBuffer.Primary].DepthTextureId, 3);
            shader.BindTexture2D("gTint", _ssrFramebuffer.ColorTextureIds[2], 4);
            shader.UniformMatrix("projectionMatrix", _mod.CApi.Render.CurrentProjectionMatrix);
            shader.UniformMatrix("invProjectionMatrix", Mat4f.Invert(invProjMatrix, _mod.CApi.Render.CurrentProjectionMatrix));
            shader.UniformMatrix("invModelViewMatrix", Mat4f.Invert(invModelViewMatrix, _mod.CApi.Render.CameraMatrixOriginf));
            shader.Uniform("zNear", uniforms.ZNear);
            shader.Uniform("zFar", uniforms.ZNear);
            shader.Uniform("sunPosition", _mod.CApi.World.Calendar.SunPositionNormalized);
            shader.Uniform("dayLight", dayLight);
            shader.Uniform("horizonFog", ambient.BlendedCloudDensity);
            shader.Uniform("fogDensityIn", ambient.BlendedFogDensity);
            shader.Uniform("fogMinIn", ambient.BlendedFogMin);
            shader.Uniform("rgbaFog", ambient.BlendedFogColor);

            GL.Disable(EnableCap.Blend);
            _platform.RenderFullscreenTriangle(_screenQuad);
            shader.Stop();
            GL.Enable(EnableCap.Blend);
            _platform.UnloadFrameBuffer(_ssrOutFramebuffer);
            
            _platform.CheckGlError("Error while calculating SSR");
        }
        
        private void OnRenderssr()
        {
            if (_ssrFramebuffer == null) return;
            if (_ssrShader == null) return;

            // bind our framebuffer
            _platform.LoadFrameBuffer(_ssrFramebuffer);
            GL.ClearBuffer(ClearBuffer.Color, 0, new []{0f, 0f, 0f, 1f});
            GL.ClearBuffer(ClearBuffer.Color, 1, new []{0f, 0f, 0f, 1f});
            GL.ClearBuffer(ClearBuffer.Color, 2, new []{0f, 0f, 0f, 1f });

            _platform.GlEnableCullFace();
            _platform.GlDepthMask(false);
            _platform.GlEnableDepthTest();
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(0, BlendingFactorSrc.OneMinusSrcAlpha, BlendingFactorDest.SrcAlpha);
            GL.BlendFunc(1, BlendingFactorSrc.OneMinusSrcAlpha, BlendingFactorDest.SrcAlpha);
            GL.BlendFunc(2, BlendingFactorSrc.OneMinusSrcAlpha, BlendingFactorDest.SrcAlpha);

            // render stuff
            _game.GlPushMatrix();
            _game.GlLoadMatrix(_mod.CApi.Render.CameraMatrixOrigin);
            var shader = _ssrShader;
            shader.Use();
            shader.UniformMatrix("projectionMatrix", _mod.CApi.Render.CurrentProjectionMatrix);
            shader.UniformMatrix("modelViewMatrix", _mod.CApi.Render.CurrentModelviewMatrix);
            shader.Uniform("dropletIntensity", _chunkRenderer.GetField<float>("curRainFall"));
            shader.Uniform("windIntensity", curWindSpeed);

            var textureIds = _chunkRenderer.GetField<int[]>("textureIds");
            
            shader.BindTexture2D("water1", waterTextures[0], 1);
            shader.BindTexture2D("water2", waterTextures[1], 2);
            shader.BindTexture2D("water3", waterTextures[2], 3);

            for (int i = 0; i < textureIds.Length; i++)
            {
                shader.BindTexture2D("terrainTex", textureIds[i], 0);

                for (int j = 0; j < _chunkRenderer.poolsByRenderPass.Length; j++)
                {
                    _platform.GlEnableCullFace();
                    if (j == (int)EnumChunkRenderPass.BlendNoCull || j == (int)EnumChunkRenderPass.OpaqueNoCull) _platform.GlDisableCullFace();

                    shader.Uniform("renderPass", j);
                    foreach (var pool in _chunkRenderer.poolsByRenderPass[j])
                    {
                        pool.Render(_game.EntityPlayer.CameraPos, "origin");
                    }
                }
            }

            shader.Stop();
            _game.GlPopMatrix();
            _platform.UnloadFrameBuffer(_ssrFramebuffer);
            
            _platform.GlDepthMask(false);
            _platform.GlToggleBlend(true);
            
            _platform.CheckGlError("Error while rendering solid liquids");
        }

        public void OnSetFinalUniforms(ShaderProgramFinal final)
        {
            if (!_enabled) return;
            if (_ssrOutFramebuffer == null) return;
            
            final.BindTexture2D("ssrScene", _ssrOutFramebuffer.ColorTextureIds[0]);
            final.BindTexture2D("ssrGlow", _ssrOutFramebuffer.ColorTextureIds[1]);
        }

        public void Dispose()
        {
            var windowsPlatform = _mod.CApi.GetClientPlatformWindows();

            if (_ssrFramebuffer != null)
            {
                // dispose the old framebuffer
                windowsPlatform.DisposeFrameBuffer(_ssrFramebuffer);
                _ssrFramebuffer = null;
            }

            if (_ssrOutFramebuffer != null)
            {
                windowsPlatform.DisposeFrameBuffer(_ssrOutFramebuffer);
                _ssrOutFramebuffer = null;
            }
            
            if (_ssrShader != null)
            {
                _ssrShader.Dispose();
                _ssrShader = null;
            }

            if (_ssrOutShader != null)
            {
                _ssrOutShader.Dispose();
                _ssrOutShader = null;
            }

            _chunkRenderer = null;
            _screenQuad = null;
        }

        public double RenderOrder => 1;

        public int RenderRange => Int32.MaxValue;
    }
}
