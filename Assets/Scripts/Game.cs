using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using ToonBoom.TBGRenderer;

public class Game : MonoBehaviour
{
    public Settings settings;

    [System.Serializable]
    public class DudeState
    {
        public Dude instance;
        public Vector2 position;
        public Vector2 velocity;
        public float lastFireTime;
    }

    public class BulletState
    {
        public GameObject instance;
        public Vector2 position;
        public Vector2 velocity;
    }

    public class BaddieState
    {
        public GameObject instance;
        public int health;
        public Vector2 position;
        public Vector2 velocity;
    }

    public class DeadBaddieState
    {
        public GameObject instance;
        public Vector2 position;
        public Vector2 velocity;
        public float angle;
        public float angularVelocity;
    }

    public float lastBaddieSpawnTime;

    public List<DudeState> dudes = new List<DudeState>();
    public List<BulletState> bullets = new List<BulletState>();
    public List<BaddieState> baddies = new List<BaddieState>();
    public List<DeadBaddieState> deadBaddies = new List<DeadBaddieState>();

    public static string[] bulletClips = new string[] {
        "Drawing_projectilec",
        "Drawing_projectileb",
        "Drawing_projectilea",
    };

    public void OnPlayerJoined(PlayerInput playerInput)
    {
        Debug.Log("Player joined: " + playerInput.playerIndex);
        dudes.Add(new DudeState
        {
            instance = playerInput.GetComponent<Dude>(),
            // Random position
            position = new Vector2(Random.Range(-5f, 5f), Random.Range(-5f, 5f)),
        });
    }

    void LateUpdate()
    {
        // Get bounds of game from bounds of the camera
        Rect cameraBounds;
        {
            var camera = Camera.main;
            var cameraHeight = camera.orthographicSize * 2f;
            var cameraWidth = cameraHeight * camera.aspect;
            cameraBounds = new Rect(
                camera.transform.position.x - cameraWidth / 2f,
                camera.transform.position.y - cameraHeight / 2f,
                cameraWidth,
                cameraHeight
            );
        }

        // Spawn baddies
        if (lastBaddieSpawnTime + 1.0f / settings.baddieSpawnRate < Time.time)
        {
            lastBaddieSpawnTime = Time.time;
            var instance = Instantiate(settings.veggieBaddyPrefab);

            Bounds totalBounds;
            {
                var renderers = instance.GetComponentsInChildren<SpriteRenderer>();
                totalBounds = new Bounds();
                foreach (var renderer in renderers)
                {
                    totalBounds.Encapsulate(renderer.bounds);
                }
            }

            // ensure baddie scale conforms to radius by getting renderer bounds and rescaling
            {
                var baddieScale = settings.baddieRadius / Mathf.Max(totalBounds.extents.x, totalBounds.extents.y);
                instance.transform.localScale = Vector3.one * baddieScale;
            }

            // set a random skin on the tbg renderer
            {
                var renderer = instance.GetComponent<TBGRenderer>();
                renderer.GroupToSkinID[0] = (ushort)(Random.Range(0, 4) + 1);
            }

            var baddie = new BaddieState
            {
                instance = instance,
                position = new Vector2(cameraBounds.xMax, Random.Range(cameraBounds.yMin + settings.baddieRadius, cameraBounds.yMax - settings.baddieRadius)),
                velocity = Vector2.left * settings.baddieSpeed,
                health = settings.baddieHealth,
            };
            baddies.Add(baddie);
        }

        // Move baddies
        for (var i = 0; i < baddies.Count; i++)
        {
            baddies[i].position += Time.deltaTime * baddies[i].velocity;
            baddies[i].instance.transform.position = baddies[i].position;

            // Once baddie exits camera bounds, destroy it
            if (!cameraBounds.Contains(baddies[i].position))
            {
                Destroy(baddies[i].instance);
                baddies.RemoveAt(i);
                i--;
            }
        }

        // Move dead baddies
        for (var i = 0; i < deadBaddies.Count; i++)
        {
            deadBaddies[i].position += Time.deltaTime * deadBaddies[i].velocity;
            deadBaddies[i].angle += Time.deltaTime * deadBaddies[i].angularVelocity;
            deadBaddies[i].instance.transform.position = deadBaddies[i].position;
            deadBaddies[i].instance.transform.rotation = Quaternion.Euler(0f, 0f, deadBaddies[i].angle);

            // Angular drag
            deadBaddies[i].angularVelocity -= Time.deltaTime * settings.deadBaddieAngularDrag * deadBaddies[i].angularVelocity;

            // Change velocity based on angular velocity (ping pong ball physics)
            var velocityTangent = new Vector2(-deadBaddies[i].velocity.y, deadBaddies[i].velocity.x);
            deadBaddies[i].velocity += Time.deltaTime * deadBaddies[i].angularVelocity * velocityTangent * settings.deadBaddieSpin;

            // Once dead baddie exits camera bounds, destroy it
            if (!cameraBounds.Contains(deadBaddies[i].position))
            {
                Destroy(deadBaddies[i].instance);
                deadBaddies.RemoveAt(i);
                i--;
            }
        }


        // Dudes
        for (var i = 0; i < dudes.Count; i++)
        {
            var currentState = dudes[i].instance.Animator.GetCurrentAnimatorStateInfo(0);
            var airborne = currentState.IsName("Body-P_jumpmid");
            var landing = currentState.IsName("Body-P_jumpland");
            var jumping = currentState.IsName("Body-P_jumpstart");
            var mobile = currentState.IsName("Body-P_idle");
            var grounded = mobile || landing;

            if (mobile)
            {
                // If jump, set the animator trigger
                if (dudes[i].instance.PlayerInput.actions["Jump"].triggered)
                {
                    dudes[i].instance.Animator.SetTrigger("Jump");
                    dudes[i].instance.Animator.Update(0f);
                }

                // Set velocity based on input
                dudes[i].velocity = dudes[i].instance.PlayerInput.actions["Move"].ReadValue<Vector2>() * settings.speed;

                // If player caught outside of camera bounds, force a jump towards the center
                if (!cameraBounds.Contains(dudes[i].position))
                {
                    dudes[i].velocity = (Vector2.zero - dudes[i].position).normalized * settings.speed;
                    dudes[i].instance.Animator.SetTrigger("Jump");
                    // Force update of animator
                    dudes[i].instance.Animator.Update(0f);
                }
            }

            // Firing
            if (grounded)
            {
                var fire = dudes[i].instance.PlayerInput.actions["Fire"].ReadValue<float>() > 0.5f;
                if (fire && dudes[i].lastFireTime + 1.0f / settings.fireRate < Time.time)
                {
                    dudes[i].lastFireTime = Time.time;
                    var instance = Instantiate(settings.fxPrefab);
                    {
                        var animator = instance.GetComponent<Animator>();
                        animator.Play(bulletClips[i]);
                        animator.Update(0f);
                    }
                    {
                        var renderer = instance.GetComponent<TBGRenderer>();
                        renderer.SetColor(settings.bulletColors[i]);
                    }
                    var bullet = new BulletState
                    {
                        instance = instance,
                        position = dudes[i].position + dudes[i].instance.PeletteSourceOffset,
                        velocity = Vector2.right * settings.bulletSpeed,
                    };
                    bullets.Add(bullet);
                }
            }

            if (mobile || airborne)
            {
                dudes[i].position += Time.deltaTime * dudes[i].velocity;
            }

            // Visuals
            {
                dudes[i].instance.transform.position = dudes[i].position;

                // Offset position upwards based on jump curve
                if (airborne)
                {
                    dudes[i].instance.transform.position += Vector3.up * settings.jumpCurve.Evaluate(currentState.normalizedTime);
                }
            }
        }

        // Bullet updates.
        var baddiesToRemove = new List<int>();
        var bulletsToDestroy = new List<int>();
        for (var i = 0; i < bullets.Count; i++)
        {
            bullets[i].position += Time.deltaTime * bullets[i].velocity;
            bullets[i].instance.transform.position = bullets[i].position;

            // Once bullet exits camera bounds, destroy it
            if (!cameraBounds.Contains(bullets[i].position))
            {
                Destroy(bullets[i].instance);
                bullets.RemoveAt(i);
                i--;
            }

            // Check all baddies, if within radius, destroy bullet and baddie
            for (var j = 0; j < baddies.Count; j++)
            {
                if (Vector2.Distance(bullets[i].position, baddies[j].position) < settings.baddieRadius)
                {
                    bulletsToDestroy.Add(i);

                    baddies[j].health = baddies[j].health - 1;
                    if (baddies[j].health <= 0)
                    {
                        // Add to dead baddies, remove from baddies, set velocity and angular velocity of dead baddy based on location and velocity of bullet
                        // We will use basic billiard ball physics to bounce the dead baddies around
                        var collisionNormal = (baddies[j].position - bullets[i].position).normalized;
                        var collisionTangent = new Vector2(-collisionNormal.y, collisionNormal.x);
                        var collisionForce = Vector2.Dot(bullets[i].velocity, collisionNormal);
                        var deadBaddie = new DeadBaddieState
                        {
                            instance = baddies[j].instance,
                            position = baddies[j].position,
                            velocity = baddies[j].velocity + collisionNormal * collisionForce / settings.deadBaddieMass,
                            angle = 0,
                            angularVelocity = -Vector2.Dot(bullets[i].velocity, collisionTangent) / settings.deadBaddieInertia * 180f / Mathf.PI,
                        };
                        deadBaddies.Add(deadBaddie);
                        baddiesToRemove.Add(j);
                    }

                    break;
                }
            }
        }
        // Remove deferred bullets/baddies.
        foreach (var bulletIndex in bulletsToDestroy)
        {
            Destroy(bullets[bulletIndex].instance);
            bullets.RemoveAt(bulletIndex);
        }
        foreach (var baddieIndex in baddiesToRemove)
        {
            baddies.RemoveAt(baddieIndex);
        }
    }
}