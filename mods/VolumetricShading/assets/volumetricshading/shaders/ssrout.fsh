#version 330 core

uniform sampler2D primaryScene;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D gDepth;

uniform mat4 projectionMatrix;
uniform mat4 invProjectionMatrix;
uniform mat4 invModelViewMatrix;

uniform vec3 sunPosition;
uniform float dayLight;
uniform float horizonFog;
uniform float fogDensityIn;
uniform float fogMinIn;
uniform vec4 rgbaFog;

in vec2 texcoord;
out vec4 outColor;
out vec4 outGlow;

#include dither.fsh
#include fogandlight.fsh
#include skycolor.fsh

float comp = 1.0-zNear/zFar/zFar;

const int maxf = 3;				//number of refinements
const float ref = 0.11;			//refinement multiplier
const float inc = 3.0;			//increasement factor at each step

vec3 nvec3(vec4 pos) {
    return pos.xyz/pos.w;
}
vec4 nvec4(vec3 pos) {
    return vec4(pos.xyz, 1.0);
}
float cdist(vec2 coord) {
	return max(abs(coord.s-0.5),abs(coord.t-0.5))*2.0;
}

vec4 raytrace(vec3 fragpos, vec3 rvector) {
    vec4 color = vec4(0.0);
    vec3 start = fragpos;
	rvector *= 1.2;
    fragpos += rvector;
	vec3 tvector = rvector;
    int sr = 0;

    for(int i = 0; i < 25; ++i) {
        vec3 pos = nvec3(projectionMatrix * nvec4(fragpos)) * 0.5 + 0.5;
        if(pos.x < 0 || pos.x > 1 || pos.y < 0 || pos.y > 1 || pos.z < 0 || pos.z > 1.0) break;
        vec3 fragpos0 = vec3(pos.st, texture(gDepth, pos.st).r);
        fragpos0 = nvec3(invProjectionMatrix * nvec4(fragpos0 * 2.0 - 1.0));
        float err = distance(fragpos,fragpos0);
		if(err < pow(length(rvector),1.175)) {
            sr++;
            if(sr >= maxf){
                float isFurther = (fragpos0.z < start.z) ? 1 : 0;
                color = pow(texture(primaryScene, pos.st), vec4(1.7));
                color.a = clamp(1.0 - pow(cdist(pos.st), 20.0), 0.0, 1.0) * isFurther;
                break;
            }
            tvector -= rvector;
            rvector *= ref;
        }
        rvector *= inc;
        tvector += rvector;
		fragpos = start + tvector;
    }
    return color;
}

void main(void) {
    vec4 positionFrom = texture(gPosition, texcoord);
    vec3 unitPositionFrom = normalize(positionFrom.xyz);
    vec3 normal = normalize(texture(gNormal, texcoord).xyz);
    vec3 pivot = normalize(reflect(unitPositionFrom, normal));

    outColor = vec4(0);

    if (positionFrom.w < 1.0) {
        vec3 positionFromUV = nvec3(projectionMatrix * positionFrom) * 0.5 + 0.5;
        vec3 positionFromDepth = vec3(positionFromUV.xy, texture(gDepth, positionFromUV.xy).r);
        positionFromDepth = nvec3(invProjectionMatrix * nvec4(positionFromDepth * 2.0 - 1.0));

        if (positionFromDepth.z > positionFrom.z + 0.01) {
            // this point in the reflection is occluded by something, maybe an item the player is holding
            return;
        }

        vec4 reflection = raytrace(positionFrom.xyz, pivot);
        vec4 skyColor = vec4(0);
        vec4 outGlow = vec4(0);
        
        pivot = (invModelViewMatrix * vec4(pivot, 0.0)).xyz;
        getSkyColorAt(pivot, sunPosition, 0.0, clamp(dayLight, 0, 1), horizonFog, skyColor, outGlow);
        skyColor.rgb = pow(skyColor.rgb, vec3(1.7));
        reflection.rgb = mix(reflection.rgb, skyColor.rgb, 0.3);
        reflection.rgb = mix(skyColor.rgb, reflection.rgb, reflection.a);

        reflection.a = 1f;

        float normalDotEye = dot(normal, unitPositionFrom);
		float fresnel = pow(clamp(1.0 + normalDotEye,0.0,1.0), 4.0);
			  fresnel = mix(0.09,1.0,fresnel);

        outColor = reflection;
        outColor.a *= (1.0f - positionFrom.w) * fresnel;
    }

    float findBright = min(max(outColor.r, max(outColor.g, outColor.b)), 0.25) - fogDensityIn;
    outGlow = vec4(findBright, 0, 0, outColor.a);

    //outColor = vec4(1.0f - positionFrom.w);
}