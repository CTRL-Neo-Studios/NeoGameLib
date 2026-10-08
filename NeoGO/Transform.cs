﻿using System;
using NeoGameLib.Common;
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

    // raw move function without lerping
    public void Move(Vector2 direction, GameTime gameTime)
    {
        _pos += direction * (float) gameTime.ElapsedGameTime.TotalSeconds;
    }

    // rotates this object so the given side points at the target. at a distance of zero it does nothing
    public void FaceTowards(Direction2D side, Vector2 position)
    {
        Vector2 dir = position - _pos;
        if (dir == Vector2.Zero) return;

        _rot = MathF.Atan2(dir.Y, dir.X) - SideAngle(side);
    }

    public void FaceTowards(Direction2D side, Transform target)
    {
        FaceTowards(side, target.Position);
    }

    public void FaceTowards(Direction2D side, NeoObject target)
    {
        FaceTowards(side, target.Transform.Position);
    }

    private static float SideAngle(Direction2D side)
    {
        return side switch
        {
            Direction2D.Right => 0f,
            Direction2D.Left => MathF.PI,
            Direction2D.Up => -MathF.PI / 2f,
            Direction2D.Down => MathF.PI / 2f,
            _ => 0f
        };
    }
    
}
