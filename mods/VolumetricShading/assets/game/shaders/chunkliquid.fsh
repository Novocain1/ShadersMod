#version 330 core

uniform sampler2D terrainTex;
uniform sampler2D depthTex;

uniform vec2 blockTextureSize;
uniform vec2 textureAtlasSize;
uniform float waterFlowCounter;
uniform vec2 frameSize;
uniform vec3 sunPosRel;
uniform vec3 sunColor;
uniform float windWaveCounter;
uniform float waterWaveCounter;
uniform float sunSpecularIntensity;
uniform float dropletIntensity = 0;

in vec4 rgba;
in vec4 rgbaFog;
in float fogAmount;
in vec2 uv;
in vec2 uvSize;
in float waterStillCounterOff;
flat in vec2 uvBase;
in vec3 fragWorldPos;
in vec3 fWorldPos;
in vec3 fragNormal;


in vec2 flowVectorf;
in float glowLevel;

flat in int waterFlags;
flat in int renderFoam;
flat in int skyExposed;

layout(location = 0) out vec4 outAccu;
layout(location = 1) out vec4 outReveal;
layout(location = 2) out vec4 outGlow;

#include fogandlight.fsh
#include noise3d.ash
#include colormap.fsh

vec2 droplethash3( vec2 p )
{
    vec2 q = vec2(dot(p,vec2(127.1,311.7)), dot(p,vec2(269.5,183.3)));
	return fract(sin(q)*43758.5453);
}

float dropletnoise(in vec2 x)
{
	if (dropletIntensity < 0.001) return 0.;
	
    x *= dropletIntensity;
    
    vec2 p = floor(x);
    vec2 f = fract(x);
    
		
	float va = 0.0;
    for( int j=-1; j<=1; j++ )
    for( int i=-1; i<=1; i++ )
    {
        vec2 g = vec2(float(i), float(j));
		vec2 o = droplethash3(p + g);
		vec2 r = ((g - f) + o.xy) / dropletIntensity;
		float d = sqrt(dot(r,r));
        
        float a = max(cos(d - waterWaveCounter * 2.7 + (o.x + o.y) * 5.0), 0.);
        a = smoothstep(0.99, 0.999, a);
        
	    float ripple = mix(a, 0., d);
        va += max(ripple, 0.);
    }
	
    return va;
}

void drawPixel(vec4 color) {
	float weight = color.a * clamp(0.03 / (1e-5 + pow(gl_FragCoord.z / 200, 4.0)), 1e-2, 3e3);
	
    // RGBA32F texture (accumulation)
    outAccu = vec4(color.rgb * color.a, color.a) * weight * 0.5; // Was * 2 but that made water behind quartz glass super visible in the night

    // R32F texture (revealage)
    // Make sure to use the red channel (and GL_RED target in your texture)
    outReveal.r = color.a; // Was *1.2 but that made lava more transparent :o
	
	float scatterAmt = calculateVolumetricScatter(fWorldPos);
	float findBright = clamp(max(color.r, max(color.g, color.b)), 0, 0.25) - fogAmount;

    outGlow = vec4(glowLevel + findBright, scatterAmt, 0, color.a);
}

void main() 
{
	// When looking through tinted glass you can clearly see the edges where we fade to sky color
	// Using this discard seems to completely fix that
	if (rgba.a < 0.005) discard;
	
	vec4 texColor;

	vec2 uvInverse = uvBase + blockTextureSize - uvSize * 0.99;

	if (length(flowVectorf) > 0.001) {
		vec2 flowVec = normalize(flowVectorf);
		vec2 maxsize = blockTextureSize - 0.1f / textureAtlasSize;
		
		vec2 uvxOffset = mod((uv - uvBase) + flowVec * waterFlowCounter * blockTextureSize, maxsize);
		
		texColor = texture(terrainTex, uvBase + uvxOffset);
	} else {
		// Needs to be rewritten to not do weird uv-inverse math but simply use a second texture so json blocks can use it too
		texColor = texture(terrainTex, uv) * waterStillCounterOff + (1-waterStillCounterOff) * texture(terrainTex, uvInverse);				
	}
	
	texColor.rgb *= getColorMapping(terrainTex).rgb;

	vec4 rgbaFinal = rgba;
#if FOAMEFFECT > 0
	rgbaFinal.a = rgba.a + max(0, texColor.a - 0.4);
#else
	rgbaFinal.a = rgba.a + texColor.a;
#endif
	float bright = (rgba.r + rgba.g + rgba.b)/3;
	
	float shadowBright = getBrightnessFromShadowMap();
	
	
	float x = gl_FragCoord.x / frameSize.x;
	float y = gl_FragCoord.y / frameSize.y;
	
	// This seems to fix being able to see rivers when looking up from inside a lake
	if (fogAmount > 0.98) discard;

	texColor *= vec4(rgbaFinal.rgb * shadowBright, rgbaFinal.a);
	

	bool doLightFoam = (waterFlags & 0x4000000) != 0;
	
#if FOAMEFFECT > 0
	if (rgbaFinal.a > 0) {
	
		// Water edge + shinyness shading effect, kinda nice
		float ownDepth = linearDepth(gl_FragCoord.z);
		float diffTotal = 0;
		int range = 2;
		for (int dx = -range; dx <= range; dx++) {
			for (int dy = -range; dy <= range; dy++) {
				float diff = ownDepth - linearDepth(texture(depthTex, vec2(x + dx/frameSize.x, y + dy/frameSize.y)).x);
				if (diff < 0.001) { // This check prevents foam not rendered when looking through grass at distant water
					diffTotal += abs(diff);
				}
			}
		}
		
		diffTotal /= (4*range * range);
		
		
		if ((waterFlags & 0x2000000) > 0) { // Bit 25
			// Lava
			float intensity = clamp(dot(fragNormal, vec3(0, 1, 0)), 0, 1) * 0.5;
			float a = fragWorldPos.x + fragWorldPos.y - 1.5 * flowVectorf.x * waterFlowCounter;
			float b = fragWorldPos.z - 1.5 * flowVectorf.y * waterFlowCounter;
			
			float diff = intensity * clamp(1 - diffTotal*1000 - gnoise(vec3(a*35, b*35, waterFlowCounter))/2 + gnoise(vec3(a*2, b*2, waterFlowCounter))/2, 0, 1);
			float noise = intensity * (gnoise(vec3(a, b, waterFlowCounter)) + 0.5) / 2;
			texColor.rgb -= vec3(2*bright*(diff * 0.3 + noise/10))/2;
			texColor.a += diff/16 + noise/4;
			
		} else {
			// Cold liquids
			
			
			// Foam
			float intensity = clamp(dot(fragNormal, vec3(0, 1, 0)), 0, 1);
			float a = fragWorldPos.x + fragWorldPos.y - 1.5 * flowVectorf.x * waterFlowCounter;
			float b = fragWorldPos.z - 1.5 * flowVectorf.y * waterFlowCounter;
			
			float noise1 = gnoise(vec3(a*35, b*35, waterFlowCounter));
			float noise2 = gnoise(vec3(a*2, b*2, waterFlowCounter));
			
			float diff = intensity * clamp(1 - diffTotal*1500 - noise1/2 + noise2/2, 0, 1);
			float noise = intensity * (gnoise(vec3(a * 0.4, b * 0.4, waterFlowCounter))/2 + gnoise(vec3(a, b, waterFlowCounter))/2 + 0.5) / 2;
			
			if (doLightFoam) {
				texColor.rgb += vec3(max(0, 2*bright*(diff * 0.3 + noise/10))) / 4;
			} else {
				texColor.rgb += vec3(max(0, 2*bright*(diff * 0.3 + noise/10))) / 2;
			}
			
			texColor.a += max(0, diff/16 + noise/4);
			
			// Droplet noise
			float f = 0;
			if (skyExposed > 0) {
				vec2 uv = 12.0 * fragWorldPos.xz / (2.0 + noise1/3000.0);
				f = dropletnoise(uv);
			}
			
			texColor.rgb *= 1 + f;

			/*
			// Specular reflection
			vec3 noisepos = vec3(fragWorldPos.x / 3 - windWaveCounter / 3, fragWorldPos.z / 3, waterWaveCounter / 12 + windWaveCounter / 6) * 0.5;
			
			//float dy = clamp(noise2 / 10, 0, 1) + gnoise(noisepos); - trippy specular rings
			
			float dy = noise2 / 20 + clamp(gnoise(noisepos) / 10, 0, 0.6);
			
			vec3 normal = normalize(vec3(dy, 1, -dy));
			
			float upness = max(0, dot(fragNormal, vec3(0,1,0))); // Only do specular reflections on up faces
			
			vec3 eye = normalize(vec3(fWorldPos.x, fWorldPos.y - 2, fWorldPos.z));
			vec3 reflectionVec = reflect(sunPosRel, normal);
			float p = dot(reflectionVec, eye);
			if (p > 0) {
				float sunb = clamp(sunPosRel.y * 10, 0, 1) * clamp(1.5 - sunPosRel.y, 0, 1) * sunSpecularIntensity;
				
				float specular = pow(p, 200) * sunb;
				
				#if SHADOWQUALITY > 0
				float weight = upness * clamp(specular * clamp(pow(shadowBright, 4), 0, 1) * clamp(1.5 * shadowIntensity, 0, 1), 0, 1);
				#else
				float weight = upness * clamp(specular * clamp(pow(shadowBright, 4), 0, 1) * clamp(1.5, 0, 1), 0, 1);
				#endif
				
				vec3 sunColf = applyFog(vec4(sunColor, 1), fogAmount).rgb;
				
				texColor.rgb = mix(texColor.rgb, sunColf + noise1 * 0.2, weight);
				texColor.a = mix(texColor.a, texColor.a + specular/2, weight);
			}*/
		}
	}
	
#endif
    
	texColor = applyFog(texColor, fogAmount);
	
	
	drawPixel(texColor);
}