// The game's hover rim (rs13::outline_rim) two texels wide, with a faint wash over the sprite

#import bevy_sprite::mesh2d_vertex_output::VertexOutput
#import rs13::outline_rim::{OutlineParams, cell_alpha}

@group(2) @binding(0) var<uniform> params: OutlineParams;
@group(2) @binding(1) var atlas_tex: texture_2d<f32>;
@group(2) @binding(2) var atlas_sampler: sampler;

const REACH: i32 = 2;
const WASH: f32 = 0.16;

@fragment
fn fragment(mesh: VertexOutput) -> @location(0) vec4<f32> {
    let lo = params.rect_min - params.margin;
    let hi = params.rect_max + params.margin;
    let uv = mix(lo, hi, mesh.uv);

    let here = cell_alpha(uv, params, atlas_tex, atlas_sampler);
    var dilated = 0.0;
    for (var dy = -REACH; dy <= REACH; dy += 1) {
        for (var dx = -REACH; dx <= REACH; dx += 1) {
            let offset = vec2<f32>(f32(dx), f32(dy)) * params.step;
            dilated = max(dilated, cell_alpha(uv + offset, params, atlas_tex, atlas_sampler));
        }
    }

    let band = clamp(dilated - here, 0.0, 1.0);
    return vec4<f32>(params.color.rgb, params.color.a * max(band, here * WASH));
}
