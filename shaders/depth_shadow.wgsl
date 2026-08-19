// Turns the above-ground sprites captured in `casters_tex` into a soft, offset,
// dark drop shadow. Drawn on a quad that sits between the floor and the walls,
// so the shadow lands on the deck and everything taller is drawn over it.

#import bevy_sprite::mesh2d_vertex_output::VertexOutput

// Look params — in WORLD units, so the shadow looks the same regardless of the
// view radius or caster-image resolution (both fold into `params.view_span`).
// Edit freely; the shader hot-reloads. Defaults match /tg/'s
// drop_shadow_filter(x=0, y=-2, size=4, color=#04080FAA).
const SHADOW_OFFSET: vec2<f32> = vec2<f32>(0.0, 2.0); // world px; +y = south
const SHADOW_BLUR: f32 = 4.0;                         // world px, box-blur radius
const SHADOW_STRENGTH: f32 = 0.667;                   // opacity at full coverage
const SHADOW_TINT: vec3<f32> = vec3<f32>(0.0012, 0.0024, 0.0048); // #04080F, linear
// Hard cap on the box-blur half-radius (source texels). Bounds worst-case taps
// at (2*MAX+1)² so zoom-in can't make the shadow pass quadratically explode.
const MAX_BLUR_RADIUS: i32 = 6;

struct DepthShadowParams {
    /// World units spanned by the captured view (the caster camera's `Fixed`
    /// cap). Converts the world-unit look params above into texels/UV.
    view_span: f32,
}

@group(2) @binding(0) var<uniform> params: DepthShadowParams;
@group(2) @binding(1) var casters_tex: texture_2d<f32>;
@group(2) @binding(2) var casters_sampler: sampler;

@fragment
fn fragment(mesh: VertexOutput) -> @location(0) vec4<f32> {
    let dims = vec2<f32>(textureDimensions(casters_tex));
    // The quad spans `view_span` world units across UV 0..1.
    let uv_per_world = 1.0 / params.view_span;

    // The shadow at this fragment is cast by the caster sitting `offset` world
    // units back along the shadow direction.
    let base_uv = mesh.uv - SHADOW_OFFSET * uv_per_world;

    // Average caster coverage (alpha) over a box kernel for a soft edge. The
    // world-unit blur radius becomes this many source texels. Clamped: taps grow
    // as (2r+1)², and zooming in shrinks view_span, which would otherwise blow r
    // (and per-pixel cost) up unbounded. Past the cap the shadow is just slightly
    // crisper when zoomed far in - cosmetically fine.
    let texel = 1.0 / dims;
    let r = min(i32(ceil(SHADOW_BLUR * uv_per_world * dims.y)), MAX_BLUR_RADIUS);
    // Bilinear box blur: step 2 texels and sample at the 2×2-block center (the
    // +1 half-texel offset), so the linear sampler averages 4 texels per tap.
    // ~4× fewer taps than a per-texel kernel for the same coverage; the hardware
    // does the inner averaging.
    var acc = 0.0;
    var samples = 0.0;
    for (var dy = -r; dy <= r; dy = dy + 2) {
        for (var dx = -r; dx <= r; dx = dx + 2) {
            let uv = base_uv + (vec2<f32>(f32(dx), f32(dy)) + 1.0) * texel;
            acc = acc + textureSampleLevel(casters_tex, casters_sampler, uv, 0.0).a;
            samples = samples + 1.0;
        }
    }
    let coverage = acc / samples;

    return vec4<f32>(SHADOW_TINT, coverage * SHADOW_STRENGTH);
}
