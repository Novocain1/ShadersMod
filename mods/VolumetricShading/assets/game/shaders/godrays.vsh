#version 330 core

uniform vec2 invFrameSizeIn;
uniform vec3 sunPosScreenIn;
uniform vec3 sunPos3dIn;
uniform vec3 playerViewVector;
uniform float iGlobalTimeIn;
uniform float directionIn;
uniform int dusk;
uniform float moonLightStrength;
uniform float sunLightStrength;
uniform float dayLightStrength;
uniform float shadowIntensity;

out vec2 texCoord;
out vec3 sunPosScreen;
out float iGlobalTime;
out float direction;
out vec3 color;

const vec3 DayColors[6] = vec3[6](
	vec3(1.0f, 0.0f, -0.2f),
	vec3(1.0f, 0.3f, 0.0f),
	vec3(1.0f, 0.5f, 0.1f),
	vec3(0.9f, 0.7f, 0.4f),
	vec3(0.6f, 0.7f, 0.7f),
	vec3(0.4f, 0.9f, 1.0f)
);

/*const vec3 DayColors[5] = vec3[5](
	vec3(1.0f, 0.0f, -0.2f),
	vec3(1.0f, 0.3f, 0.0f),
	vec3(1.0f, 0.5f, 0.1f),
	vec3(1.0f, 0.8f, 0.4f),
	vec3(1.0f, 0.8f, 0.7f)
);*/

void main(void)
{
	// https://rauwendaal.net/2014/06/14/rendering-a-screen-covering-triangle-in-opengl/
	float x = -1.0 + float((gl_VertexID & 1) << 2);
    float y = -1.0 + float((gl_VertexID & 2) << 1);
    gl_Position = vec4(x, y, 0, 1);
    texCoord = vec2((x+1.0) * 0.5, (y + 1.0) * 0.5);
	
	sunPosScreen = sunPosScreenIn;
	iGlobalTime = iGlobalTimeIn;
	
	//float sunPlrAngle = getScattering(dot(sunPos3dIn, playerViewVector));
	
	direction = dot(sunPos3dIn, playerViewVector) >= 0 ? 1 : -1;

	vec3 moonColor = vec3(0.2, 0.4, 0.7) * moonLightStrength;

	float height = pow(min(max(sunPos3dIn.y*1.5f, 0.0f), 1f), 1f);
	float actualScale = height*6.0f;
	float cmpH = min(floor(actualScale), 5.0f);
	float cmpH1 = min(floor(actualScale)+1.0f, 5.0f);

	vec3 temp = DayColors[int(cmpH)];
	vec3 temp2 = DayColors[int(cmpH1)];
	vec3 sunlight = mix(temp, temp2, fract(actualScale));
	//sunlight = DayColors[5];

	vec3 sunColor = sunlight * min(pow(shadowIntensity, 2.0f), 1.0f) * 1.2f; // midday
	color = moonColor;
	if (sunLightStrength > 0.15f) {
		color = sunColor;
	} else if (sunLightStrength > 0.05f) {
		color = mix(moonColor, sunColor, (sunLightStrength - 0.05f) / 0.05f);
	}

	// http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJtYXgoMSwxLjc1KigxLTYqYWJzKHgtMC4yMikpKSIsImNvbG9yIjoiIzAwMDAwMCJ9LHsidHlwZSI6MTAwMCwid2luZG93IjpbIi0xIiwiMSIsIjAiLCIyIl19XQ--
	//float dawnMul = max(1, (1-dusk) * 2 * (1 - 6*abs(sunPos3dIn.y - 0.1)));
	
	// Intensity is determined by how directly the player is looking at the sun
	// above intensity 0.8 we get godrays where they shouldn't be o.o
	//intensity = clamp(sunPlrAngle * dawnMul, 0, 0.8);
	//intensity = 0.8;
}