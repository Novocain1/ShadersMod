#ifndef DYNLIGHTS
    #define DYNLIGHTS 0
#endif
#if SHADOWQUALITY > 0
out float blockBrightness;
#endif

out float glowLevel;
out vec3 blockLight;

uniform float flatFogDensity;
uniform float flatFogStart;
uniform float viewDistance;
uniform float viewDistanceLod0;
uniform float glitchStrengthFL;

#if DYNLIGHTS > 0
uniform vec3[DYNLIGHTS] pointLights;
uniform vec3[DYNLIGHTS] pointLightColors;
uniform int pointLightQuantity;
#endif


vec4 applyLightWithoutPointLight(vec4 sunColor, vec4 blockColor, float bGlow) {
	float bSun = (sunColor.r + sunColor.g + sunColor.b)/3;
	float bBlock = (blockColor.r + blockColor.g + blockColor.b)/3;
	
	// 1. Mix colors according to their brightness (very bright light has more influence on the color)
	vec4 rgba = (2 * bSun * sunColor + bBlock * blockColor) / (2 * bSun + bBlock);
	
	// 2. Fix brightness
	rgba *= max(bGlow, max(bSun, bBlock)) / ((rgba.r + rgba.g + rgba.b) / 3);
	
#if SHADOWQUALITY > 0
	blockBrightness = clamp(max(bGlow, bBlock) - bSun/2, 0, 1);
#endif
	
	// 4. Always fully opaque
	rgba.a = 1;
	
	return rgba;
}

// sunColor = color of the ambient light, or the sun color really
// lightColor = rgb is block light, a is sun light brightness
vec4 applyLight(vec3 ambientColor, vec4 lightColor, int renderFlags, vec4 worldPos) {
	
	float bGlow = glowLevel = (renderFlags & 0xff) / 256.0;
	float contrast = 1.05;	

	vec3 blockLightColor = lightColor.rgb;
	vec3 sunLightColor = lightColor.a * ambientColor.rgb;

#if DYNLIGHTS == 0
	return applyLightWithoutPointLight(vec4(sunLightColor ,1), vec4(blockLightColor,1), bGlow);
#else

	// Sun brightness
	float bSun = (sunLightColor.r + sunLightColor.g + sunLightColor.b)/3;
	// Block brightness
	float bBlock = (blockLightColor.r + blockLightColor.g + blockLightColor.b)/3;
	// Point light brightness
	float bPoint = 0;
	
	bBlock /= max(1, glitchStrengthFL * 2);
	
	float bPointBrightSum = 0;
	vec3 pointColSum = vec3(0);
	for (int i = 0; i < pointLightQuantity; i++) {
		vec4 lightVec = -vec4(worldPos.x - pointLights[i].x, worldPos.y - pointLights[i].y, worldPos.z - pointLights[i].z, 1);
		vec3 color = pointLightColors[i];
		
		float dist = pow(1.35, length(lightVec));
		//float dist = length(lightVec);
		float bright = (color.r + color.g + color.b);
		float strength = min(bright/3, bright / dist);
		
		bPoint = max(bPoint, strength);
		bPointBrightSum += strength;
		
		pointColSum.r +=  color.r * strength;
		pointColSum.g +=  color.g * strength;
		pointColSum.b +=  color.b * strength;
	}

	// Light up all caves
	//bBlock = 1;

	if (bPointBrightSum > 0) {
		pointColSum.rgb /= max(1, bPointBrightSum);
	}
	
	bPoint /= max(1, glitchStrengthFL * 2);
	
	
	// 1. Mix colors according to their brightness (very bright light has more influence on the color)
	vec3 rgba = (2 * bSun * sunLightColor + bBlock * blockLightColor + bPoint * pointColSum) / (2 * bSun + bBlock + bPoint);
	
	// 2. Fix brightness
	float bMax = max(bGlow, max(bPoint, max(bSun, bBlock)));
	
	blockLight = rgba;
	
#if SHADOWQUALITY > 0
	blockBrightness = clamp(max(bGlow, max(bPoint, bBlock)) - bSun/2, 0, 1);
#endif
	
	
	rgba *= bMax / ((rgba.r + rgba.g + rgba.b) / 3);
	
	rgba *= 1 + bGlow/4;
	
	rgba *= contrast;
	
	return vec4(rgba, 1);
#endif
}



float getFogLevel(vec4 worldPos, float fogMin, float fogDensity) {
	float depth = length(worldPos);
	float clampedDepth = min(250, depth);
	float heightDiff = worldPos.y - flatFogStart;
	
	//float extraDistanceFog = max(-flatFogDensity * flatFogStart / (160 + heightDiff * 3), 0);   // heightDiff*3 seems to fix distant mountains being supper fogged on most flat fog values
	// ^ this breaks stuff. Also doesn't seem to be needed? Seems to work fine without
	
	float extraDistanceFog = max(-flatFogDensity * clampedDepth * (flatFogStart) / 60, 0); // div 60 was 160 before, at 160 thick flat fog looks broken when looking at trees
	
	float distanceFog = 1 - 1 / exp(clampedDepth * fogDensity + extraDistanceFog);
	
	float flatFog = 1 - 1 / exp(heightDiff * flatFogDensity); 
	
	float val = max(flatFog, distanceFog);
	float nearnessToPlayer = clamp((8-depth)/16, 0, 0.8);
	val = max(min(0.04, val), val - nearnessToPlayer);
	
	// Needs to be added after so that underwater fog still gets applied. 
	val += fogMin; 
	
	return clamp(val, 0, 1);
}


vec4 applyFog(vec4 worldPos, vec4 rgbaPixel, vec4 rgbaFog, float fogMin, float fogDensity) {
	float amount = getFogLevel(worldPos, fogMin, fogDensity);
	return vec4(mix(rgbaPixel.rgb, rgbaFog.rgb, amount), rgbaPixel.a * rgbaFog.a);
}



vec3 unpackNormal(int flags) {
	int x = (flags >> 1) & 0x7;
	int y = (flags >> 5) & 0x7;
	int z = (flags >> 9) & 0x7;
	
	int signx = flags & 1;
	int signy = (flags >> 4) & 1;
	int signz = (flags >> 8) & 1;
	
	return vec3(
		(1.0 - signx * 2) * x / 7.0,
		(1.0 - signy * 2) * y / 7.0,
		(1.0 - signz * 2) * z / 7.0
	);
}