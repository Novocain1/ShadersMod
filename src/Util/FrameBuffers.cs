using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Util;

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