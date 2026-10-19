using System;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoRendering.RenderFeatures.Sprite;

/// <summary>
/// A component that draws extra layers behind or in front of the NeoSpriteRenderer on the same object.
/// </summary>
/// <remarks>
/// <para>Add the NeoSpriteRenderer first. The feature attaches to it on awake, so the render bus calls the feature in every pass that draws the sprite.</para>
/// <para>Draw calls happen in the order of: OnDrawBehind -> Sprite -> OnDrawFront. Camera culling & the camera's view matrix also applies to the render feature.</para>
/// <para>A disabled feature does not draw, so it's important that you use OnFeatureAwake/OnFeatureDestroy for setup/cleanup.</para>
/// </remarks>
public abstract class NeoSpriteRenderFeature : NeoComponent
{
    /// <summary>The sprite renderer on the same object.</summary>
    public NeoSpriteRenderer Renderer { get; private set; }

    public sealed override void OnAwake()
    {
        Renderer = ParentObject.GetComponent<NeoSpriteRenderer>();
        if (Renderer is null)
            throw new InvalidOperationException("NeoSpriteRenderer is required when using NeoSpriteRenderFeature");

        Renderer.Features.Add(this);
        OnFeatureAwake();
    }

    public sealed override void OnDestroy()
    {
        Renderer?.Features.Remove(this);
        OnFeatureDestroy();
    }

    protected virtual void OnFeatureAwake() { }
    protected virtual void OnFeatureDestroy() { }

    /// <summary>Draws before the sprite</summary>
    public virtual void OnDrawBehind(SpriteBatch spriteBatch, in NeoSpriteDrawInfo info) { }

    /// <summary>Draws after the sprite</summary>
    public virtual void OnDrawFront(SpriteBatch spriteBatch, in NeoSpriteDrawInfo info) { }
}
