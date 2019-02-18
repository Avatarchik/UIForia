using System;

namespace SVGX {

    [Flags]
    public enum FillMode {

        Color = 0,
        Texture = 1 << 0,
        Gradient = 1 << 1,
        Tint = 1 << 2,
        Pattern = 1 << 3,
        TextureGradient = Texture | Gradient,
        TextureTint = Texture | Tint

    }

}