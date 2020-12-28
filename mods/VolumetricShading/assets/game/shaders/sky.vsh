#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertexPositionIn;
layout(location = 1) in vec4 vertexColor;

uniform vec4 rgbaFogIn;
uniform vec3 rgbaAmbientIn;
uniform mat4 projectionMatrix;
uniform mat4 modelViewMatrix;

#if GODRAYS > 0
uniform vec3 lightPosition;
#endif

out vec3 vertexPosition;
flat out vec4 rgbaFog;

#include shadowcoords.vsh
#include fogandlight.vsh

void main()
{
	vertexPosition = vertexPositionIn;
	rgbaFog = rgbaFogIn;
	vec4 cameraPos = modelViewMatrix * vec4(vertexPosition, 1.0);
	
	calcShadowMapCoords(modelViewMatrix, vec4(vertexPosition, 1.0));

#if GODRAYS > 0
	prepareVolumetricLighting(lightPosition);
#endif

    gl_Position = projectionMatrix * cameraPos;
}