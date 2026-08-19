// One space-backdrop layer: a seamless, tiled texture (a nebula image or a
// generated star tile) on a camera-following quad at a deep negative z. Every
// layer is its own quad/material; the renderer alpha-blends them by z (farther
// layers lower), so this shader only places one layer. The layer's parallax
// rate (`move_factor`: small = distant, barely shifts), tile span, and drift
// come from the recipe in `mod.rs`. Hot-reloads.

#import bevy_sprite::mesh2d_vertex_output::VertexOutput

struct LayerParams {
    // World-space camera centre — the parallax origin.
    camera_pos: vec2<f32>,
    // World span of one tile repeat (texture px * scale), per axis.
    tile: vec2<f32>,
    // Constant drift, world px/s.
    drift: vec2<f32>,
    // Elapsed seconds.
    time: f32,
    // Fraction of camera motion this layer follows (small = distant).
    move_factor: f32,
}

@group(2) @binding(0) var<uniform> p: LayerParams;
@group(2) @binding(1) var tex: texture_2d<f32>;
@group(2) @binding(2) var tex_sampler: sampler;

@fragment
fn fragment(mesh: VertexOutput) -> @location(0) vec4<f32> {
    let world = mesh.world_position.xy;
    let shifted = world - p.camera_pos * (1.0 - p.move_factor) + p.drift * p.time;
    // Repeat-wrapping sampler tiles it; alpha is preserved so the pipeline
    // composites this layer over the ones below.
    return textureSample(tex, tex_sampler, shifted / p.tile);
}
