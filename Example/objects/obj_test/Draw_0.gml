var px = room_width / 4;
var py = room_height / 3;
var offset = 160;

draw_text(px, py - offset, "sh_example_grayscale_noise\n(import full)");
shader_set(sh_example_grayscale_noise);
draw_sprite(spr_shady, 0, px, py);
shader_reset();

draw_text(px * 2, py - offset, "sh_example_inverse\n(inline macro)");
shader_set(sh_example_inverse);
draw_sprite(spr_shady, 0, px * 2, py);
shader_reset();

draw_text(px * 3, py - offset, "sh_example_grayscale_inverse\n(import full + inline macro)");
shader_set(sh_example_grayscale_inverse);
draw_sprite(spr_shady, 0, px * 3, py);
shader_reset();

draw_text(px, py * 2 + offset, "sh_example_flip_base\n(import partial)");
shader_set(sh_example_flip_base);
draw_sprite(spr_shady, 0, px, py * 2);
shader_reset();

draw_text(px * 2, py * 2 + offset, "sh_example_flip_variant_red\n(import partial + 1 param variant)");
shader_set(sh_example_flip_variant_red);
draw_sprite(spr_shady, 0, px * 2, py * 2);
shader_reset();

draw_text(px * 3, py * 2 + offset, "sh_example_flip_variant_noiseblue\n(import partial + 2 params variant)");
shader_set(sh_example_flip_variant_noiseblue);
draw_sprite(spr_shady, 0, px * 3, py * 2);
shader_reset();