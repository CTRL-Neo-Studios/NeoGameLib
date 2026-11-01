using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoInput;
using NeoGameLib.NeoMapping;
using NeoGameLib.NeoMapping.DurableActions;
using NeoGameLib.NeoMapping.DurableActions.Context;

namespace NeoGameLib.NeoUI.Elements;

/// <summary>
/// A clickable text element. Reads pointer state from NeoInput.Input, so it needs a running game.
/// </summary>
/// <remarks>
/// <para>OnClick fires when the left mouse button is pressed and released over this element's layout bounds.</para>
/// </remarks>
public class NeoButton : NeoUIElement
{
    private bool _pressedOver;

    private SpriteFont? _font;
    private string? _fontPath;

    /// <summary>
    /// either use the configured font or defaults back to Inter
    /// </summary>
    public SpriteFont? Font
    {
        get => _font ??= NeoUIAssets.Load<SpriteFont>(_fontPath);
        set { _font = value; _fontPath = null; }
    }

    /// <summary>Content path of the font (e.g. "fonts/ui"), loaded through NeoGame.Assets on first use. This is what UI files store.</summary>
    [NeoAsset(typeof(SpriteFont))]
    public string? FontPath
    {
        get => _fontPath;
        set { _fontPath = value; _font = null; }
    }

    private SpriteFont? ActiveFont => Font ?? NeoBuiltinContent.DefaultFont;

    public string Text { get; set; } = "";
    public Color Color { get; set; } = Color.White;
    public Color HoverColor { get; set; } = Color.LightGray;
    public Color PressedColor { get; set; } = Color.Gray;

    /// <summary>True when the mouse cursor is inside this element's bounds.</summary>
    public bool Hovered { get; private set; }

    /// <summary>Invokes on left-button release inside the element after a click.</summary>
    public Action? OnClick { get; set; }

    /// <summary>
    /// Data-driven click handlers
    /// </summary>
    public List<INeoAction> OnClickActions { get; } = new();

    private void Click()
    {
        OnClick?.Invoke();

        NeoActionContext ctx = new(null, null, this);
        for (int i = 0; i < OnClickActions.Count; i++)
            OnClickActions[i].Invoke(ctx);
    }

    protected override Vector2 MeasureContent() => (ActiveFont is { } font && !string.IsNullOrEmpty(Text)) ? font.MeasureString(Text) : Vector2.Zero;

    public override void OnUpdate(GameTime gameTime)
    {
        bool over = ContainsScreenPoint(Input.MousePosition);
        Hovered = over;

        if (over && Input.LeftMouseButton.IsPressed()) _pressedOver = true;

        if (Input.LeftMouseButton.IsReleased())
        {
            if (over && _pressedOver) Click();

            _pressedOver = false;
        }
    }

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        if (ActiveFont is not { } font || string.IsNullOrEmpty(Text)) return;

        Color color = Color;
        if (Hovered) color = Input.LeftMouseButton.IsDown() ? PressedColor : HoverColor;

        Vector2 size = font.MeasureString(Text);
        spriteBatch.DrawString(font, Text, ScreenTransform.LayoutPosition + ScreenTransform.LayoutSize / 2f, color, 0f, size / 2f, Vector2.One, SpriteEffects.None, 0f);
    }
}
