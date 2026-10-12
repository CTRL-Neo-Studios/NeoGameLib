using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoInput;

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

    public SpriteFont? Font { get; set; }
    public string Text { get; set; } = "";
    public Color Color { get; set; } = Color.White;
    public Color HoverColor { get; set; } = Color.LightGray;
    public Color PressedColor { get; set; } = Color.Gray;

    /// <summary>True when the layout size comes from the measured text size. When false, Size controls layout.</summary>
    public bool AutoSize { get; set; } = true;

    /// <summary>True when the mouse cursor is inside this element's layout bounds this tick.</summary>
    public bool Hovered { get; private set; }

    /// <summary>Fires on left-button release inside the element after a press that started in it.</summary>
    public Action? OnClick { get; set; }

    protected override Vector2 MeasureSize() => AutoSize
        ? (Font != null && !string.IsNullOrEmpty(Text) ? Font.MeasureString(Text) : Vector2.Zero)
        : base.MeasureSize();

    public override void OnUpdate(GameTime gameTime)
    {
        Vector2 mouse = Input.MousePosition;
        bool over = mouse.X >= LayoutPosition.X &&
                    mouse.X <= LayoutPosition.X + LayoutSize.X &&
                    mouse.Y >= LayoutPosition.Y && 
                    mouse.Y <= LayoutPosition.Y + LayoutSize.Y;
        Hovered = over;

        if (over && Input.LeftMouseButton.IsPressed()) _pressedOver = true;

        if (Input.LeftMouseButton.IsReleased())
        {
            if (over && _pressedOver) OnClick?.Invoke();

            _pressedOver = false;
        }
    }

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        if (Font == null || string.IsNullOrEmpty(Text)) return;

        Color color = Color;
        if (Hovered) color = Input.LeftMouseButton.IsDown() ? PressedColor : HoverColor;

        Vector2 size = Font.MeasureString(Text);
        spriteBatch.DrawString(Font, Text, LayoutPosition + LayoutSize / 2f, color, 0f, size / 2f, Vector2.One, SpriteEffects.None, 0f);
    }
}
