// A rim just outside a sprite's silhouette: sample the sprite's own atlas
// cell at 8 neighbour offsets and light the pixels where a neighbour is
// opaque but this spot is not. The host is slightly larger than the sprite
// (params.margin), and anything outside the cell reads as transparent so
// neighbouring atlas cells never leak into the rim. Shared by the world quad
// and the HUD node, which differ only in their vertex stage and bind group

#define_import_path rs13::outline_rim

struct OutlineParams {
    color: vec4<f32>,
    rect_min: vec2<f32>,
    rect_max: vec2<f32>,
    step: vec2<f32>,
    margin: vec2<f32>,
}

// `textureSampleLevel` rather than `textureSample`: the cell-bounds branch
// makes the control flow non-uniform
fn cell_alpha(
    uv: vec2<f32>,
    params: OutlineParams,
    atlas_tex: texture_2d<f32>,
    atlas_sampler: sampler,
) -> f32 {
    if any(uv < params.rect_min) || any(uv > params.rect_max) {
        return 0.0;
    }
    return textureSampleLevel(atlas_tex, atlas_sampler, uv, 0.0).a;
}

// `host_uv` runs 0..1 across the host, which hangs params.margin past the
// cell on every side
fn rim(
    host_uv: vec2<f32>,
    params: OutlineParams,
    atlas_tex: texture_2d<f32>,
    atlas_sampler: sampler,
) -> vec4<f32> {
    let lo = params.rect_min - params.margin;
    let hi = params.rect_max + params.margin;
    let uv = mix(lo, hi, host_uv);

    let here = cell_alpha(uv, params, atlas_tex, atlas_sampler);
    var dilated = 0.0;
    for (var dy = -1; dy <= 1; dy += 1) {
        for (var dx = -1; dx <= 1; dx += 1) {
            if dx == 0 && dy == 0 {
                continue;
            }
            let offset = vec2<f32>(f32(dx), f32(dy)) * params.step;
            dilated = max(dilated, cell_alpha(uv + offset, params, atlas_tex, atlas_sampler));
        }
    }

    let band = clamp(dilated - here, 0.0, 1.0);
    return vec4<f32>(params.color.rgb, params.color.a * band);
}
