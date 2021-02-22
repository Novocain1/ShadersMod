using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace Shaders
{
    public static class ReflectionHelper
    {
        public static ClientMain GetClient(this ICoreClientAPI api) => api.World as ClientMain;

        public static ClientPlatformAbstract GetClientPlatformAbstract(this ClientMain client) => client.GetField<ClientPlatformAbstract>("Platform");

        public static ClientPlatformWindows GetClientPlatformWindows(this ClientMain client) => client.GetClientPlatformAbstract() as ClientPlatformWindows;

        public static ClientPlatformAbstract GetClientPlatformAbstract(this ICoreClientAPI api) => api.GetClient().GetClientPlatformAbstract();
        
        public static ClientPlatformWindows GetClientPlatformWindows(this ICoreClientAPI api) => api.GetClient().GetClientPlatformWindows();

        public static ChunkRenderer GetChunkRenderer(this ClientMain client) => client.GetField<ChunkRenderer>("chunkRenderer");

        public static MeshRef GetScreenQuad(this ClientPlatformWindows platform) => platform.GetField<MeshRef>("screenQuad");

        public static void TriggerOnlyOnMouseUp(this GuiElementSlider slider, bool trigger = true) => slider.CallMethod("TriggerOnlyOnMouseUp", trigger);
    }
}
