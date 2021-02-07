using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace Shaders
{
    public static class FrameBuffers
    {
        public static void SetupDepthTexture(this FrameBufferRef fbRef)
        {
            GL.BindTexture(TextureTarget.Texture2D, fbRef.DepthTextureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32,
                fbRef.Width, fbRef.Height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int) TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int) TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int) TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int) TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D, fbRef.DepthTextureId, 0);
        }

        public static int LoadTextureArray(this IRenderAPI rapi, AssetLocation[] locations, int width, int height)
        {
            return ((rapi as RenderAPIGame).Api.World as ClientMain).LoadTextureArray(locations, width, height);
        }

        public static int LoadTextureArray(this ClientMain game, AssetLocation[] locations, int width, int height)
        {
            var Platform = game.GetField<ClientPlatformWindows>("Platform");

            BitmapRef[] bmps = new BitmapRef[locations.Length];

            for (int i = 0; i < locations.Length; i++)
            {
                var name = locations[i].WithPathPrefixOnce("textures/");

                byte[] assetData = Platform.AssetManager.TryGet(name)?.Data;

                if (assetData == null) return 0;

                bmps[i] = Platform.BitmapCreateFromPng(assetData, assetData.Length);
            }

            int id = Platform.LoadTextureArray(bmps, width, height, true);

            for (int i = 0; i < bmps.Length; i++)
            {
                bmps[i].Dispose();
            }

            return id;
        }

        public static int LoadTextureArray(this ClientPlatformWindows platform, IBitmap[] bmps, int width, int height, bool bmpext = false, bool linearMag = false, int clampMode = 0, bool generateMipmaps = false)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != RuntimeEnv.MainThreadId)
            {
                throw new InvalidOperationException("Texture uploads must happen in the main thread. We only have one OpenGL context.");
            }

            int id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, id);

            if (platform.ENABLE_ANISOTROPICFILTERING)
            {
                float maxAniso = GL.GetFloat((GetPName)ExtTextureFilterAnisotropic.MaxTextureMaxAnisotropyExt);
                GL.TexParameter(TextureTarget.Texture2DArray, (TextureParameterName)ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt, maxAniso);
            }

            if (clampMode == 1)
            {
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            }

            if (bmpext)
            {
                IntPtr[] pointers = new IntPtr[bmps.Length];

                for (int i = 0; i < bmps.Length; i++)
                {
                    var bmp = bmps[i];
                    var bmpExt = bmp as BitmapExternal;

                    Bitmap bitmap = bmpExt.bmp;
                    BitmapData bmp_data = bitmap.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    pointers[i] = bmp_data.Scan0;

                    bitmap.UnlockBits(bmp_data);
                }

                GL.TexImage2D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, pointers);
            }
            else
            {
                byte[,] texels = new byte[bmps.Length, width * height * 4];

                for (int i = 0; i < bmps.Length; i++)
                {
                    var bmp = bmps[i];

                    for (int j = 0; j < bmp.Pixels.Length; j++)
                    {
                        byte[] bytes = ColorUtil.ToRGBABytes(bmp.Pixels[j]);

                        texels[i, j * 4 + 0] = bytes[0];
                        texels[i, j * 4 + 1] = bytes[1];
                        texels[i, j * 4 + 2] = bytes[2];
                        texels[i, j * 4 + 3] = bytes[3];
                    }
                }

                GL.TexImage2D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, texels);
            }


            if (platform.ENABLE_MIPMAPS && generateMipmaps)
            {
                int[] MipMapCount = new int[1];
                GL.GetTexParameter(TextureTarget.Texture2DArray, GetTextureParameter.TextureMaxLevel, out MipMapCount[0]);

                if (MipMapCount[0] == 0)
                {
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                }
                else
                {
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapLinear);
                }

                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, linearMag ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureLodBias, 0f);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, ClientSettings.MipMapLevel);

                // Causes seams between blocks :/
                //GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt, 4);

                GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
            }
            else
            {
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, linearMag ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest);
            }

            return id;
        }

        public static void BindArrayTexture(this IShaderProgram prog1, string samplerName, int textureArrayId, int textureNumber)
        {
            var prog = prog1 as ShaderProgramBase;
            if (prog != null)
            {
                GL.Uniform1(prog.uniformLocations[samplerName], textureNumber);
                GL.ActiveTexture((TextureUnit)(33984 + textureNumber));
                GL.BindTexture(TextureTarget.Texture2DArray, textureArrayId);

                if (prog.customSamplers.ContainsKey(samplerName))
                {
                    GL.BindSampler(textureNumber, prog.customSamplers[samplerName]);
                }

                if (prog.clampTToEdge)
                {
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, Convert.ToInt32(TextureWrapMode.ClampToEdge));
                }
            }
        }

        public static void SetupTextures(this FrameBufferRef fbRef, int[] vIds, int[] cIds, bool depth = true)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbRef.FboId);
            
            if (depth) fbRef.SetupDepthTexture();
            fbRef.SetupVertexTextures(vIds);
            fbRef.SetupColorTextures(cIds);
            
            DrawBuffersEnum[] attachments = ArrayUtil.CreateFilled(vIds.Length + cIds.Length, i => DrawBuffersEnum.ColorAttachment0 + i);
            GL.DrawBuffers(attachments.Length, attachments);
            
            fbRef.CheckStatus();
        }

        public static void SetupVertexTextures(this FrameBufferRef fbRef, params int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                fbRef.SetupVertexTexture(indices[i]);
            }
        }

        public static void SetupVertexTexture(this FrameBufferRef fbRef, int textureId)
        {
            GL.BindTexture(TextureTarget.Texture2D, fbRef.ColorTextureIds[textureId]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, fbRef.Width, fbRef.Height, 0,
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

        public static void SetupColorTextures(this FrameBufferRef fbRef, params int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                fbRef.SetupColorTexture(indices[i]);
            }
        }

        public static void SetupColorTexture(this FrameBufferRef fbRef, int textureId)
        {
            GL.BindTexture(TextureTarget.Texture2D, fbRef.ColorTextureIds[textureId]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, fbRef.Width, fbRef.Height, 0, PixelFormat.Rgba, PixelType.UnsignedShort, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + textureId,
                TextureTarget.Texture2D, fbRef.ColorTextureIds[textureId], 0);
        }

        public static void CheckStatus(this FrameBufferRef fbRef) => CheckStatus();

        public static void CheckStatus()
        {
            var errorCode = GL.Ext.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (errorCode != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("Could not create framebuffer: " + errorCode);
            }
        }
    }
}