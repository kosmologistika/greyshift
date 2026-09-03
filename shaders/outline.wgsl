// The world rim: a quad under the outlined sprite, see rs13::outline_rim

#import bevy_sprite::mesh2d_vertex_output::VertexOutput
#import rs13::outline_rim::{OutlineParams, rim}

@group(2) @binding(0) var<uniform> params: OutlineParams;
@group(2) @binding(1) var atlas_tex: texture_2d<f32>;
@group(2) @binding(2) var atlas_sampler: sampler;

@fragment
fn fragment(mesh: VertexOutput) -> @location(0) vec4<f32> {
    return rim(mesh.uv, params, atlas_tex, atlas_sampler);
}
