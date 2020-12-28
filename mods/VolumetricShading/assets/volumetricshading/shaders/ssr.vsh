#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertexPositionIn;
layout(location = 1) in vec2 uvIn;
// rgb = block light, a=sun light level
layout(location = 2) in vec4 rgbaLightIn;
// Bits 0-7: Glow level
// Bits 8-10: Z-Offset
// Bit 11: Wind waving yes/no
// Bit 12: Water waving yes/no
layout(location = 3) in int renderFlags;

layout(location = 4) in vec2 flowVector;

// Bit 0: Should animate yes/no
// Bit 1: Should texture fade yes/no
// Bits 8-15: x-Distance to upper left corner, where 255 = size of the block texture
// Bits 16-24: y-Distance to upper left corner, where 255 = size of the block texture
// Bit 25: Lava yes/no
// Bit 26: Weak foamy yes/no
// Bit 27: Weak Wavy yes/no
layout(location = 5) in int waterFlagsIn;

// Bits 0..7 = season map index
// Bits 8..11 = climate map index
// Bits 12 = Frostable bit
// Bits 13, 14, 15 = free \o/
// Bits 16-23 = temperature
// Bits 24-31 = rainfall
layout(location = 6) in int colormapData;

uniform vec3 origin;
uniform mat4 projectionMatrix;
uniform mat4 modelViewMatrix;
uniform int renderPass;

out vec2 uv;
out vec4 worldPos;
out vec4 fragPosition;
out vec4 gnormal;
flat out int flags;
flat out int waterFlags;
flat out int skyExposed;

#include vertexwarp.vsh
#include fogandlight.vsh

bool isOpaquePass = renderPass == 0;
bool isOpaqueNoCullPass = renderPass == 1;
bool isBlendNoCullPass = renderPass == 2;
bool isTransparentPass = renderPass == 3;
bool isLiquidPass = renderPass == 4;
bool isTopSoilPass = renderPass == 5;
bool isMetaPass = renderPass == 6;

void main(void)
{
	uv = uvIn;
	flags = renderFlags >> 8;
	worldPos = vec4(vertexPositionIn + origin, 1.0);
	skyExposed = (flags >> 13) & 1;

	bool weakWave = ((waterFlagsIn & (1<<27)) > 0);

	float div = weakWave ? 40 : 90;
	float yBefore = worldPos.y;
	
	vec3 fragNormal = unpackNormal(renderFlags >> 15);

	if (isLiquidPass) worldPos = applyLiquidWarping((waterFlagsIn & 0x2000000) == 0, worldPos, div);
	else worldPos = applyVertexWarping(renderFlags, worldPos);
	
	worldPos.xyz += fragNormal * 0.001;

	vec4 cameraPos = modelViewMatrix * worldPos;
	
	gl_Position = projectionMatrix * cameraPos;
	

    fragPosition = cameraPos;
	gnormal = modelViewMatrix * vec4(fragNormal.xyz, 0);
    waterFlags = waterFlagsIn;

	// We pretend the decal is closer to the camera to enforce it always being drawn on top
	// Required e.g. when water is besides stairs or slabs
	gl_Position.w += 0.0008 / max(0.1, gl_Position.z);
}