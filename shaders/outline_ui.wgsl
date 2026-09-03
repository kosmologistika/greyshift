// The HUD rim: a node over the icon of a cell standing for the outlined
// item, see rs13::outline_rim

#import bevy_ui::ui_vertex_output::UiVertexOutput
#import rs13::outline_rim::{OutlineParams, rim}

@group(1) @binding(0) var<uniform> params: OutlineParams;
@group(1) @binding(1) var atlas_tex: texture_2d<f32>;
@group(1) @binding(2) var atlas_sampler: sampler;

@fragment
fn fragment(in: UiVertexOutput) -> @location(0) vec4<f32> {
    return rim(in.uv, params, atlas_tex, atlas_sampler);
}
