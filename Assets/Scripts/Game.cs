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
        public float lastHitTime;
    }

    public class BulletState
    {
        public GameObject instance;
        public Color color;
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
        public float deathTime;
        public GameObject instance;
        public Vector2 position;
        public Vector2 velocity;
        public float angle;
        public float angularVelocity;
    }

    public class FXState
    {
        public GameObject instance;
        public GameObject parent;
        public float startTime;
    }

    public float lastBaddieSpawnTime;

    public HashSet<DudeState> dudes = new HashSet<DudeState>();
    public HashSet<BulletState> bullets = new HashSet<BulletState>();
    public HashSet<BaddieState> baddies = new HashSet<BaddieState>();
    public HashSet<DeadBaddieState> deadBaddies = new HashSet<DeadBaddieState>();
    public HashSet<FXState> fxInstances = new HashSet<FXState>();

    public static string[] bulletClips = new string[] {
        "Drawing_projectilec",
        "Drawing_projectileb",
        "Drawing_projectilea",
    };

    public static string shieldFXClip = "Drawing_shield";

    public static string[] hitFXClip = new string[] {
        "Drawing_spherecrack",
        "Drawing_sphereimpact",
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
        var baddiesToDestroy = new HashSet<BaddieState>();
        var baddiesToRemove = new HashSet<BaddieState>();
        var deadBaddiesToDestroy = new HashSet<DeadBaddieState>();
        var bulletsToDestroy = new HashSet<BulletState>();
        var fxInstancesToDestroy = new HashSet<FXState>();

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

        // Update fx.
        foreach (var fxInstance in fxInstances)
        {
            // current state is "Empty" then destroy.
            var animator = fxInstance.instance.GetComponent<Animator>();
            var currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (currentState.IsName("Empty") || fxInstance.parent == null)
            {
                fxInstancesToDestroy.Add(fxInstance);
            }
            else
            {
                fxInstance.instance.transform.position = fxInstance.parent.transform.position;
            }
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

            float baddieMoveScale;
            // ensure baddie scale conforms to radius by getting renderer bounds and rescaling
            {
                var baddieScale = settings.baddieRadius / Mathf.Max(totalBounds.extents.x, totalBounds.extents.y);
                var scale = Random.Range(0.5f, 2.0f);
                instance.transform.localScale = Vector3.one * baddieScale * scale;
                baddieMoveScale = 1 / scale;
            }

            // set a random skin on the tbg renderer
            {
                var renderer = instance.GetComponent<TBGRenderer>();
                renderer.GroupToSkinID[0] = (ushort)(Random.Range(0, 1) + 4 + 1);
            }

            // set a random time offset on the animator
            {
                var animator = instance.GetComponent<Animator>();
                animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, Random.Range(0f, 1f));
            }

            var baddie = new BaddieState
            {
                instance = instance,
                position = new Vector2(cameraBounds.xMax, Random.Range(cameraBounds.yMin + settings.baddieRadius, cameraBounds.yMax - settings.baddieRadius)),
                velocity = Vector2.left * settings.baddieSpeed * baddieMoveScale,
                health = settings.baddieHealth,
            };
            baddies.Add(baddie);
        }

        // Move baddies
        foreach (var baddie in baddies)
        {
            baddie.position += Time.deltaTime * baddie.velocity;

            // Compose position - z is based on y
            Vector3 position = baddie.position;
            position.z = position.y;
            baddie.instance.transform.position = position;

            // Once baddie exits camera bounds, destroy it
            if (!cameraBounds.Contains(baddie.position))
            {
                baddiesToDestroy.Add(baddie);
            }
        }

        // Move dead baddies
        foreach (var deadBaddie in deadBaddies)
        {
            // Once dead baddie exits camera bounds, destroy it
            if (deadBaddie.instance == null || !cameraBounds.Contains(deadBaddie.position))
            {
                deadBaddiesToDestroy.Add(deadBaddie);
                break;
            }

            deadBaddie.position += Time.deltaTime * deadBaddie.velocity;
            deadBaddie.angle += Time.deltaTime * deadBaddie.angularVelocity;
            deadBaddie.instance.transform.position = deadBaddie.position;
            deadBaddie.instance.transform.rotation = Quaternion.Euler(0f, 0f, deadBaddie.angle);

            // Angular drag
            deadBaddie.angularVelocity -= Time.deltaTime * settings.deadBaddieAngularDrag * deadBaddie.angularVelocity;

            // Change velocity based on angular velocity (ping pong ball physics)
            var velocityTangent = new Vector2(-deadBaddie.velocity.y, deadBaddie.velocity.x);
            deadBaddie.velocity += Time.deltaTime * deadBaddie.angularVelocity * velocityTangent * settings.deadBaddieSpin;

            // Flash white after hit
            FlashAllSprites(deadBaddie.instance, Color.Lerp(Color.clear, Color.white, settings.hitFlashCurve.Evaluate(Time.time - deadBaddie.deathTime)));
        }


        // Dudes
        var i = 0;
        foreach (var dude in dudes)
        {
            var currentState = dude.instance.Animator.GetCurrentAnimatorStateInfo(0);
            var airborne = currentState.IsName("Body-P_jumpmid");
            var landing = currentState.IsName("Body-P_jumpland");
            var jumping = currentState.IsName("Body-P_jumpstart");
            var idle = currentState.IsName("Body-P_idle");
            var grounded = idle || landing;

            if (idle)
            {
                // If jump, set the animator trigger
                if (dude.instance.PlayerInput.actions["Jump"].triggered)
                {
                    dude.instance.Animator.SetTrigger("Jump");
                    dude.instance.Animator.Update(0f);
                }

                // Set velocity based on input
                dude.velocity = dude.instance.PlayerInput.actions["Move"].ReadValue<Vector2>() * settings.speed;

                // If player caught outside of camera bounds, force a jump towards the center
                if (!cameraBounds.Contains(dude.position))
                {
                    dude.velocity = (Vector2.zero - dude.position).normalized * settings.speed;
                    dude.instance.Animator.SetTrigger("Jump");
                    // Force update of animator
                    dude.instance.Animator.Update(0f);
                }
            }

            // Firing
            if (grounded)
            {
                var fire = dude.instance.PlayerInput.actions["Fire"].ReadValue<float>() > 0.5f;
                if (fire && dude.lastFireTime + 1.0f / settings.fireRate < Time.time)
                {
                    dude.lastFireTime = Time.time;
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
                        color = settings.bulletColors[i],
                        position = dude.position + dude.instance.PeletteSourceOffset,
                        velocity = Vector2.right * settings.bulletSpeed,
                    };
                    bullets.Add(bullet);
                }
            }

            if (landing)
            {
                dude.position += Time.deltaTime * dude.velocity * settings.landingSpeedMultiplier;
            }

            if (idle)
            {
                dude.position += Time.deltaTime * dude.velocity;
            }

            if (airborne)
            {
                dude.position += Time.deltaTime * dude.velocity * settings.jumpSpeedMultiplier;
            }

            if (grounded)
            {
                // Check all baddies for collision, if they hit, play the "Body-P_hit" state
                foreach (var baddie in baddies)
                {
                    if (Vector2.Distance(baddie.position, dude.position) < settings.baddieRadius + settings.dudeRadius)
                    {
                        // Play hit animation
                        var animator = dude.instance.GetComponent<Animator>();
                        animator.Play("Body-P_hit", 0, 0f);

                        // Destroy baddie
                        baddiesToDestroy.Add(baddie);

                        // Set hit time on dude
                        dude.lastHitTime = Time.time;
                        break;
                    }
                }
            }

            // Visuals
            {
                // compose position - z is based on y
                Vector3 position = dude.position;
                position.z = dude.position.y;
                dude.instance.transform.position = position;

                // Offset position upwards based on jump curve
                if (airborne)
                {
                    dude.instance.transform.position += Vector3.up * settings.jumpCurve.Evaluate(currentState.normalizedTime);
                }

                // Flash white after hit
                FlashAllSprites(dude.instance.gameObject, Color.Lerp(Color.clear, Color.white, settings.hitFlashCurve.Evaluate(Time.time - dude.lastHitTime)));
            }
            i++;
        }

        // Bullet updates.
        foreach (var bullet in bullets)
        {
            bullet.position += Time.deltaTime * bullet.velocity;
            bullet.instance.transform.position = bullet.position;

            // Once bullet exits camera bounds, destroy it
            if (!cameraBounds.Contains(bullet.position))
            {
                bulletsToDestroy.Add(bullet);
                break;
            }

            // Check all baddies, if within radius, destroy bullet and baddie
            foreach (var baddie in baddies)
            {
                if (Vector2.Distance(bullet.position, baddie.position) < settings.baddieRadius)
                {
                    bulletsToDestroy.Add(bullet);

                    baddie.health = baddie.health - 1;
                    if (baddie.health <= 0)
                    {
                        // Add to dead baddies, remove from baddies, set velocity and angular velocity of dead baddy based on location and velocity of bullet
                        // We will use basic billiard ball physics to bounce the dead baddies around
                        var collisionNormal = (baddie.position - bullet.position).normalized;
                        var collisionTangent = new Vector2(-collisionNormal.y, collisionNormal.x);
                        var collisionForce = Vector2.Dot(bullet.velocity, collisionNormal);
                        var deadBaddie = new DeadBaddieState
                        {
                            deathTime = Time.time,
                            instance = baddie.instance,
                            position = baddie.position,
                            velocity = baddie.velocity + collisionNormal * collisionForce / settings.deadBaddieMass,
                            angle = 0,
                            angularVelocity = -Vector2.Dot(bullet.velocity, collisionTangent) / settings.deadBaddieInertia * 180f / Mathf.PI,
                        };
                        deadBaddies.Add(deadBaddie);
                        baddiesToRemove.Add(baddie);

                        // Trigger "Hit" on baddie animator
                        baddie.instance.GetComponent<Animator>().SetTrigger("Hit");
                        SpawnFx(baddie.instance, bullet.color, settings.baddieRadius, hitFXClip[Random.Range(0, hitFXClip.Length)]);

                        // Change color on tbg renderer to bullet color - blend in dark color
                        var color = bullet.color * new Color(0.85f, 0.85f, 0.85f, 1f);
                        baddie.instance.GetComponent<TBGRenderer>().SetColor(color);
                    }
                    else
                    {
                        // Spawn fx for shield
                        SpawnFx(baddie.instance, bullet.color, settings.baddieRadius, shieldFXClip);
                    }

                    break;
                }
            }
        }

        foreach (var baddie in baddiesToDestroy)
        {
            Destroy(baddie.instance);
            baddies.Remove(baddie);
        }
        foreach (var baddie in baddiesToRemove)
        {
            baddies.Remove(baddie);
        }
        foreach (var deadBaddie in deadBaddiesToDestroy)
        {
            Destroy(deadBaddie.instance);
            deadBaddies.Remove(deadBaddie);
        }
        foreach (var bullet in bulletsToDestroy)
        {
            Destroy(bullet.instance);
            bullets.Remove(bullet);
        }
        foreach (var fxInstance in fxInstancesToDestroy)
        {
            Destroy(fxInstance.instance);
            fxInstances.Remove(fxInstance);
        }
    }
    public void SpawnFx(GameObject parent, Color color, float radius, string clip)
    {
        // Spawn fx for hit
        var fxInstance = new FXState
        {
            instance = Instantiate(settings.fxPrefab),
            parent = parent,
            startTime = Time.time,
        };
        // Position
        {
            fxInstance.instance.transform.position = parent.transform.position;
        }
        // Animator
        {
            var animator = fxInstance.instance.GetComponent<Animator>();
            animator.Play(clip);
            animator.Update(0f);
        }
        // Renderer color
        {
            var renderer = fxInstance.instance.GetComponent<TBGRenderer>();
            renderer.SetColor(color);
        }
        {
            fxInstance.instance.transform.localScale = Vector3.one * settings.fxScale;
        }
        fxInstances.Add(fxInstance);
    }

    public void FlashAllSprites(GameObject parent, Color color)
    {
        var sprites = parent.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sprite in sprites)
        {
            sprite.material.SetColor("_Flash", color);
        }
    }
}