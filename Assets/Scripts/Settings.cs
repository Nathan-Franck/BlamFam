using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Settings", menuName = "ScriptableObjects/Settings", order = 1)]
public class Settings : ScriptableObject
{
    public GameObject dudePrefab;
    public GameObject veggieBaddyPrefab;
    public GameObject fxPrefab;
    public float speed;
    public float jumpSpeedMultiplier;
    public float landingSpeedMultiplier;
    public AnimationCurve jumpCurve;
    public float fireRate;
    public float bulletSpeed;
    public Color[] bulletColors;
    public float baddieSpawnRate;
    public float baddieSpeed;
    public float baddieRadius;
    public int baddieHealth;
    public float deadBaddieMass;
    public float deadBaddieInertia;
    public float deadBaddieSpin;
    public float deadBaddieAngularDrag;
    public float fxScale;
}
