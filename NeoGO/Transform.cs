using System;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoGO;

// i hate encapsulation i hate java i hate everything
[System.Serializable]
public class Transform
{
    private Vector2 _pos = new(0);
    private float _rot = 0f;
    private Vector2 _scale = new(1f);
    
    public Vector2 Position
    {
        get => _pos;
        set => _pos = value;
    }

    public float Rotation
    {
        get => _rot;
        set => _rot = value;
    }

    public Vector2 Up
    {
        get => new(MathF.Sin(_rot), -MathF.Cos(_rot));
    }

    public Vector2 Down
    {
        get => -Up;
    }

    public Vector2 Right
    {
        get => new(MathF.Cos(_rot), MathF.Sin(_rot));
    }

    public Vector2 Left
    {
        get => -Right;
    }

    public Vector2 Scale
    {
        get => _scale;
        set => _scale = value;
    }

    public Transform(Vector2 pos, float rot = 0f)
    {
        this._pos = pos;
        this._rot = rot;
    }

    public Transform(Vector2 pos, Vector2 scale)
    {
        this._pos = pos;
        this._scale = scale;
    }

    public Transform() : this(new Vector2(0)) {}
    
}
