using System;
using UnityEngine;

public interface IHittable
{
    void OnHit(IHitter hitter, Vector3 position);
    event Action<IHittable> OnDespawn;
}

public interface IHitter
{
    int Damage { get; }
    Vector3 Direction { get; }
    float Force { get; }
}