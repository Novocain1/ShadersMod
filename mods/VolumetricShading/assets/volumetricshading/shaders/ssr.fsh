#version 330 core


uniform sampler2D terrainTex;
uniform vec3 playerpos;
uniform float windWaveCounter;
uniform float waterWaveCounter;
uniform int renderPass;
uniform float dropletIntensity = 0;

in vec2 uv;
in vec4 worldPos;
in vec4 fragPosition;
in vec4 gnormal;
flat in int applyPuddles;
flat in int flags;
flat in int waterFlags;
flat in int shinyOrSkyExposed;

layout(location = 0) out vec4 outGPosition;
layout(location = 1) out vec4 outGNormal;

bool isOpaquePass = renderPass == 0;
bool isOpaqueNoCullPass = renderPass == 1;
bool isBlendNoCullPass = renderPass == 2;
bool isTransparentPass = renderPass == 3;
bool isLiquidPass = renderPass == 4;
bool isTopSoilPass = renderPass == 5;
bool isMetaPass = renderPass == 6;

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

vec3 ghash( vec3 p ) // replace this by something better
{
	p = vec3( dot(p,vec3(127.1,311.7, 74.7)),
			  dot(p,vec3(269.5,183.3,246.1)),
			  dot(p,vec3(113.5,271.9,124.6)));

	return -1.0 + 2.0*fract(sin(p)*43758.5453123);
}

float gnoise( in vec3 p )
{
    vec3 i = floor( p );
    vec3 f = fract( p );
	
	vec3 u = f*f*(3.0-2.0*f);

    return 0.7 * mix( mix( mix( dot( ghash( i + vec3(0.0,0.0,0.0) ), f - vec3(0.0,0.0,0.0) ), 
                          dot( ghash( i + vec3(1.0,0.0,0.0) ), f - vec3(1.0,0.0,0.0) ), u.x),
                     mix( dot( ghash( i + vec3(0.0,1.0,0.0) ), f - vec3(0.0,1.0,0.0) ), 
                          dot( ghash( i + vec3(1.0,1.0,0.0) ), f - vec3(1.0,1.0,0.0) ), u.x), u.y),
                mix( mix( dot( ghash( i + vec3(0.0,0.0,1.0) ), f - vec3(0.0,0.0,1.0) ), 
                          dot( ghash( i + vec3(1.0,0.0,1.0) ), f - vec3(1.0,0.0,1.0) ), u.x),
                     mix( dot( ghash( i + vec3(0.0,1.0,1.0) ), f - vec3(0.0,1.0,1.0) ), 
                          dot( ghash( i + vec3(1.0,1.0,1.0) ), f - vec3(1.0,1.0,1.0) ), u.x), u.y), u.z );
}

void main() 
{
    // apply waves
    float div = ((waterFlags & (1<<27)) > 0) ? 90 : 10;
    float wind = ((waterFlags & 0x2000000) == 0) ? 1 : 0;
    vec3 noisepos = vec3((worldPos.x + playerpos.x) - windWaveCounter / 6, (worldPos.z + playerpos.z), waterWaveCounter / 12 + wind * windWaveCounter / 6);
	
    bool isNotWater = (waterFlags & (1<<25)) > 0;
    
    bool skyExposed = shinyOrSkyExposed > 0 && isLiquidPass;
    bool shiny = shinyOrSkyExposed > 0 && !isLiquidPass;
    bool applypuddles = applyPuddles > 0;

    float noise = !isLiquidPass || isNotWater ? 0 : (gnoise(noisepos) / div);
    float mul = 0;

    // Droplet noise
    float f = 0;
    float puddles = 1.0;
    bool water1 = isLiquidPass && !isNotWater;
    
    if (skyExposed || applypuddles){
        vec2 coord = 12.0 * (worldPos.xz + playerpos.xz) / (2.0 + noise/3000.0);
        f = dropletnoise(coord);
    }

    if(dropletIntensity > 0.0 && !water1 && applypuddles) {
        puddles = gnoise(vec3((worldPos.xyz + playerpos.xyz) + sin(waterWaveCounter * 0.01) * 2.0));
        puddles += gnoise(vec3((worldPos.xyz + playerpos.xyz + sin(windWaveCounter * 0.01)) * 32.0));
    }
    
    vec4 color = texture(terrainTex, uv);

    mul = water1 || shiny ? 0.0 : puddles;

    mul = color.a < 0.01 ? max(f, 0.999999) : mul;
    vec3 worldPos = fragPosition.xyz;

	outGPosition = vec4(worldPos, mul);

	outGNormal = gnormal + vec4(noise - dFdx(f), 0.0, noise - dFdy(f), mul);
}