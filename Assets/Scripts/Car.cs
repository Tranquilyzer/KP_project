using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class Car : MonoBehaviour
{
    public float hp;
    public static Car Occupied;
    public bool playerInCar = false;
    public GameObject player;
    public CinemachineCamera cm;
    public Sprite destroyCarSprite;
    public GameObject explosion_Particle;
    public float acceleration = 28f;
    public float brakeForce = 40f;
    public float maxSpeed = 18f;
    public float reverseSpeed = 7f;
    public float coastDrag = 6f;
    public float exitOffset = 3.2f;

    [Header("Водитель NPC")]
    [Tooltip("Полицейская машина: таран — розыск, угон — 2 звезды.")]
    public bool isPolice;
    [Tooltip("Префаб NPC-водителя. Если задан, машина при старте едет сама (трафик).")]
    public GameObject driverPrefab;
    [Tooltip("С какой скорости удар в полицейскую машину даёт розыск.")]
    public float policeRamSpeed = 3f;
    [HideInInspector] public NPC driver;
    [HideInInspector] public List<NPC> passengers = new List<NPC>();
    public float wheelbase = 3.4f;

    public float maxSteerAngle = 34f;
 
    public float highSpeedSteerAngle = 14f;
    public float steerSpeed = 160f;
 
    public float grip = 24f;
    public float driftGrip = 7f;
    public float driftStartSpeed = 9f;
    public float slideDrag = 8f;

 
    public float cameraZoomOut = 5f;
    public float cameraZoomSpeed = 2.5f;

    
    public float runOverSpeed = 8f;

    public float runOverDamage = 60f;

    [Header("Взрыв")]
    public float explosionRadius = 5f;
  
    public float explosionDamage = 80f;
    public float explosionForce = 15f;
    public float shakeStrength = 0.8f;
    public float shakeDuration = 0.6f;
 
    public GameObject explosionEffectPrefab;

    [Header("Аварии")]

    public float crashSpeed = 7f;
    [Tooltip("Урон за каждую единицу скорости сверх порога.")]
    public float crashDamage = 4f;
    public float crashShake = 0.4f;

    [Header("Звуки")]
    public AudioClip explosionSound;
 
    public AudioClip engineSound;
    public float engineVolume = 0.6f;
    public float engineMinPitch = 0.8f;
    public float engineMaxPitch = 1.8f;
  
    public AudioClip crashSound;

    public AudioClip brakeSound;
    public float brakeVolume = 0.7f;

    [Header("Повреждения")]
  
    public float maxHp;
    [Range(0f, 1f)] public float smokeAt = 1f / 3f;
   
    [Range(0f, 1f)] public float fireAt = 0.1f;
  
    public float burnDamagePerSecond = 5f;
 
    public Vector2 damageEffectOffset = new Vector2(0f, 1f);

    public Transform[] smokePoints;

    Rigidbody2D rb;
    float baseCameraSize;
    bool restoringCamera;
    Vector2 lastVelocity;
    float lastAngularVelocity;
    bool exploded;
    readonly List<ParticleSystem> smokeEffects = new List<ParticleSystem>();
    readonly List<ParticleSystem> fireEffects = new List<ParticleSystem>();
    int enteredFrame = -1;
    AudioSource engineSource;
    AudioSource brakeSource;
    bool skidding;
    float skidHoldUntil;
    GameObject lastAttacker;
    float throttle;
    float steer;
    float currentSteer;
    float aiThrottle;
    float aiSteer;

    public bool HasAiDriver => driver != null && !playerInCar && !exploded;
    public bool IsWrecked => exploded || hp <= 0f;
    public float ForwardSpeed => rb != null ? Vector2.Dot(rb.linearVelocity, transform.up) : 0f;

    void Awake()
    {
        if (maxHp <= 0f)
            maxHp = hp;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (engineSound != null)
        {
            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.clip = engineSound;
            engineSource.loop = true;
            engineSource.playOnAwake = false;
            engineSource.spatialBlend = 0f;
            engineSource.volume = 0f;
            engineSource.pitch = engineMinPitch;
        }

        if (brakeSound != null)
        {
            brakeSource = gameObject.AddComponent<AudioSource>();
            brakeSource.clip = brakeSound;
            brakeSource.loop = true;
            brakeSource.playOnAwake = false;
            brakeSource.spatialBlend = 0f;
            brakeSource.volume = 0f;
        }
    }

    void Start()
    {
        cm = GameObject.Find("CinemachineCamera").GetComponent<CinemachineCamera>();
        if (cm != null)
            baseCameraSize = cm.Lens.OrthographicSize;

        if (driverPrefab != null && driver == null && !playerInCar)
        {
            GameObject driverObject = Instantiate(driverPrefab, transform.position, Quaternion.identity);
            NPC npc = driverObject.GetComponent<NPC>();
            if (npc != null)
                EnterAsDriver(npc);
            else
                Destroy(driverObject);
        }
    }

    // ---------- Водитель и пассажиры NPC ----------

    // Вызывается CarAI каждый кадр: газ/тормоз и руль, как от клавиатуры.
    public void SetAiInput(float throttleInput, float steerInput)
    {
        aiThrottle = Mathf.Clamp(throttleInput, -1f, 1f);
        aiSteer = Mathf.Clamp(steerInput, -1f, 1f);
    }

    public bool EnterAsDriver(NPC npc)
    {
        if (npc == null || driver != null || playerInCar || IsWrecked)
            return false;

        driver = npc;
        npc.HideInCar(this);
        if (GetComponent<CarAI>() == null)
            gameObject.AddComponent<CarAI>();
        return true;
    }

    public void AddPassenger(NPC npc)
    {
        if (npc == null)
            return;
        passengers.Add(npc);
        npc.HideInCar(this);
    }

    // Все NPC выходят из машины по бокам.
    public void UnloadAll()
    {
        int index = 0;
        if (driver != null)
        {
            NPC npc = driver;
            driver = null;
            npc.LeaveCar(ExitPosition(index++));
        }
        foreach (NPC npc in passengers)
        {
            if (npc != null)
                npc.LeaveCar(ExitPosition(index++));
        }
        passengers.Clear();
        aiThrottle = 0f;
        aiSteer = 0f;
    }

    public Vector2 ExitPosition(int index)
    {
        float side = index % 2 == 0 ? 1f : -1f;
        float back = (index / 2) * 1.2f;
        return (Vector2)transform.position + (Vector2)transform.right * exitOffset * side - (Vector2)transform.up * back;
    }

    void LateUpdate()
    {
        UpdateCameraZoom();
    }

    void UpdateCameraZoom()
    {
        if (cm == null || PlayerHealth.CameraLocked || (!playerInCar && !restoringCamera))
            return;

        float target = baseCameraSize;
        if (playerInCar && rb != null)
        {
            float speed01 = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(maxSpeed, 0.01f));
            target += cameraZoomOut * speed01;
        }

        float size = Mathf.Lerp(cm.Lens.OrthographicSize, target, 1f - Mathf.Exp(-cameraZoomSpeed * Time.deltaTime));
        if (!playerInCar && Mathf.Abs(size - target) < 0.01f)
        {
            size = target;
            restoringCamera = false;
        }

        cm.Lens.OrthographicSize = size;
    }

    void Update()
    {
        UpdateDamageEffects();
        UpdateEngineSound();
        UpdateBrakeSound();

        if (playerInCar && Input.GetKeyDown(KeyCode.F) && Time.frameCount != enteredFrame)
            ExitCar();

        if (playerInCar && hp > 0f)
        {
            throttle = Input.GetAxisRaw("Vertical");
            steer = Input.GetAxisRaw("Horizontal");
        }
        else if (HasAiDriver && hp > 0f)
        {
            throttle = aiThrottle;
            steer = aiSteer;
        }
        else
        {
            throttle = 0f;
            steer = 0f;
        }
    }

    void UpdateEngineSound()
    {
        if (engineSource == null)
            return;

        bool running = playerInCar && !exploded;
        float speed01 = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(maxSpeed, 0.01f));
        float load = Mathf.Max(speed01, Mathf.Abs(throttle) * 0.5f);
        float targetVolume = running ? engineVolume * Mathf.Lerp(0.6f, 1f, load) : 0f;
        float targetPitch = Mathf.Lerp(engineMinPitch, engineMaxPitch, speed01);

        engineSource.volume = Mathf.MoveTowards(engineSource.volume, targetVolume, 2f * Time.deltaTime);
        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, 1f - Mathf.Exp(-5f * Time.deltaTime));

        if (running && !engineSource.isPlaying)
            engineSource.Play();
        else if (!running && engineSource.isPlaying && engineSource.volume <= 0.001f)
            engineSource.Stop();
    }

    void UpdateBrakeSound()
    {
        if (brakeSource == null)
            return;

        // Занос определяется каждый физический кадр и может на миг пропадать.
        // Держим звук ещё немного после последнего заноса, чтобы он не рвался.
        if (skidding)
            skidHoldUntil = Time.time + 0.3f;

        bool inCar = playerInCar && !exploded;
        bool active = inCar && Time.time < skidHoldUntil;
        float speed01 = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(maxSpeed, 0.01f));
        float targetVolume = active ? brakeVolume * Mathf.Clamp01(speed01 * 1.5f) : 0f;
        brakeSource.loop = true;
        brakeSource.volume = Mathf.MoveTowards(brakeSource.volume, targetVolume, (active ? 8f : 4f) * Time.deltaTime);

        // Пока игрок в машине, звук крутится по кругу без остановки — меняется только громкость.
        // Так при каждом новом заносе он не начинается заново с начала файла.
        if (active && !brakeSource.isPlaying)
        {
            brakeSource.time = Random.Range(0f, brakeSource.clip.length * 0.9f);
            brakeSource.Play();
        }
        else if (!inCar && brakeSource.isPlaying && brakeSource.volume <= 0.001f)
            brakeSource.Stop();
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        bool driving = (playerInCar || HasAiDriver) && hp > 0f;
        float dt = Time.fixedDeltaTime;
        Vector2 forward = transform.up;
        Vector2 right = transform.right;
        Vector2 velocity = rb.linearVelocity;
        float forwardSpeed = Vector2.Dot(velocity, forward);
        float lateralSpeed = Vector2.Dot(velocity, right);

        if (!driving && Mathf.Abs(forwardSpeed) < 0.08f && Mathf.Abs(lateralSpeed) < 0.08f)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            currentSteer = Mathf.MoveTowards(currentSteer, 0f, steerSpeed * dt);
            lastVelocity = Vector2.zero;
            skidding = false;
            lastAngularVelocity = 0f;
            return;
        }

        forwardSpeed = DriveSpeed(forwardSpeed, driving, dt);
        currentSteer = UpdateSteer(driving ? steer : 0f, forwardSpeed, dt);

        float targetYaw = YawRate(forwardSpeed, currentSteer);
        float appliedYaw = Mathf.Lerp(
            rb.angularVelocity * Mathf.Deg2Rad,
            targetYaw,
            1f - Mathf.Exp(-12f * dt));

        float slipAngle = Mathf.Atan2(lateralSpeed, Mathf.Max(Mathf.Abs(forwardSpeed), 0.75f));
        if (Mathf.Abs(slipAngle) > 55f * Mathf.Deg2Rad)
            appliedYaw *= 0.6f;

        float cog = Mathf.Max(wheelbase, 0.01f) * 0.5f;
        float desiredLateral = -appliedYaw * cog;
        float gripAccel = GripAccel(forwardSpeed, currentSteer, targetYaw);
        bool sliding = gripAccel < grip * 0.75f;
        bool braking = (throttle > 0.01f && forwardSpeed < -0.15f) || (throttle < -0.01f && forwardSpeed > 0.15f);
        skidding = driving && Mathf.Abs(forwardSpeed) > 2f && (braking || (sliding && Mathf.Abs(lateralSpeed) > 2f));

        lateralSpeed = Mathf.MoveTowards(lateralSpeed, desiredLateral, gripAccel * dt);
        if (sliding)
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, slideDrag * dt);

        forwardSpeed = Mathf.Clamp(forwardSpeed, -reverseSpeed, maxSpeed);

        Vector2 nextVelocity = forward * forwardSpeed + right * lateralSpeed;
        float speedCap = maxSpeed * 1.08f;
        if (nextVelocity.sqrMagnitude > speedCap * speedCap)
            nextVelocity = nextVelocity.normalized * speedCap;

        rb.angularVelocity = appliedYaw * Mathf.Rad2Deg;
        rb.linearVelocity = nextVelocity;
        lastVelocity = nextVelocity;
        lastAngularVelocity = rb.angularVelocity;
    }

    float DriveSpeed(float forwardSpeed, bool driving, float dt)
    {
        if (!driving)
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeForce * dt);
        else if (throttle > 0.01f && forwardSpeed < -0.15f)
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeForce * dt);
        else if (throttle < -0.01f && forwardSpeed > 0.15f)
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeForce * dt);
        else if (throttle > 0.01f)
        {
            float load = Mathf.Clamp01(Mathf.Max(forwardSpeed, 0f) / Mathf.Max(maxSpeed, 0.01f));
            float accel = Mathf.Lerp(acceleration, acceleration * 0.35f, load);
            forwardSpeed += accel * throttle * dt;
        }
        else if (throttle < -0.01f)
            forwardSpeed -= acceleration * 0.5f * dt;
        else
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDrag * dt);

        return Mathf.Clamp(forwardSpeed, -reverseSpeed, maxSpeed);
    }

    float UpdateSteer(float steerInput, float forwardSpeed, float dt)
    {
        float speed01 = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / Mathf.Max(maxSpeed, 0.01f));
        float limit = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speed01);
        float target = steerInput * limit;
        return Mathf.MoveTowards(currentSteer, target, steerSpeed * dt);
    }

    float YawRate(float forwardSpeed, float steerAngle)
    {
        float steerRad = steerAngle * Mathf.Deg2Rad;
        return -forwardSpeed * Mathf.Tan(steerRad) / Mathf.Max(wheelbase, 0.01f);
    }

    float GripAccel(float forwardSpeed, float steerAngle, float yawRate)
    {
        float steer01 = maxSteerAngle <= 0.01f ? 0f : Mathf.Abs(steerAngle) / maxSteerAngle;
        float speed01 = Mathf.InverseLerp(driftStartSpeed, maxSpeed, Mathf.Abs(forwardSpeed));
        float cornerAccel = Mathf.Abs(forwardSpeed * yawRate);
        float overGrip = Mathf.Clamp01(cornerAccel / Mathf.Max(grip, 0.01f) - 0.85f);
        float slide = Mathf.Clamp01(steer01 * speed01 * 0.7f + overGrip);
        return Mathf.Lerp(grip, driftGrip, slide);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Игрок на машине врезался в полицейскую — розыск.
        Car otherCar = collision.collider.GetComponentInParent<Car>();
        if (otherCar != null && otherCar != this && otherCar.isPolice && playerInCar &&
            collision.relativeVelocity.magnitude >= policeRamSpeed)
            WantedLevel.Instance.RaiseTo(1);

        NPC npc = collision.collider.GetComponentInParent<NPC>();
        if (npc == null)
        {
            TakeCrashDamage(collision);
            return;
        }
        if (npc.IsDead)
            return;

        float speed = lastVelocity.magnitude;
        if (speed < runOverSpeed)
            return;

        SetNpcCollision(npc, false);
        rb.linearVelocity = lastVelocity;
        rb.angularVelocity = lastAngularVelocity;

        float damage = runOverDamage * Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.01f));
        npc.RunOver(damage, gameObject);
        StartCoroutine(RestoreNpcCollision(npc));
    }

    void TakeCrashDamage(Collision2D collision)
    {
        if (exploded || collision.collider.isTrigger || collision.contactCount == 0)
            return;
        if (collision.collider.GetComponentInParent<Bullet>() != null)
            return;
        if (collision.collider.GetComponentInParent<PlayerController>() != null)
            return;

        float impact = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
        if (impact < crashSpeed)
            return;

        if (playerInCar)
            CameraShake.Shake(transform.position, crashShake * Mathf.Clamp01(impact / Mathf.Max(maxSpeed, 0.01f)), 0.3f);
        Sound.Play(crashSound, transform.position, Mathf.Clamp01(0.4f + impact / Mathf.Max(maxSpeed, 0.01f)));
        takeHp((impact - crashSpeed) * crashDamage);
    }

    void SetNpcCollision(NPC npc, bool enabled)
    {
        Collider2D[] carColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] npcColliders = npc.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D carCollider in carColliders)
        {
            foreach (Collider2D npcCollider in npcColliders)
                Physics2D.IgnoreCollision(carCollider, npcCollider, !enabled);
        }
    }

    IEnumerator RestoreNpcCollision(NPC npc)
    {
        yield return new WaitForSeconds(1f);
        while (npc != null && !npc.IsDead && OverlapsNpc(npc))
            yield return new WaitForSeconds(0.2f);

        if (npc != null && !npc.IsDead)
            SetNpcCollision(npc, true);
    }

    bool OverlapsNpc(NPC npc)
    {
        Collider2D[] carColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] npcColliders = npc.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D carCollider in carColliders)
        {
            foreach (Collider2D npcCollider in npcColliders)
            {
                if (carCollider.enabled && npcCollider.enabled && carCollider.Distance(npcCollider).isOverlapped)
                    return true;
            }
        }
        return false;
    }

    public void takeHp(float damage, GameObject attacker = null)
    {
        if (attacker != null && attacker != gameObject)
            lastAttacker = attacker;

        hp -= damage;
        if (hp <= 0 && !exploded)
            Explode();
    }

    void UpdateDamageEffects()
    {
        if (maxHp <= 0f)
            return;

        float ratio = hp / maxHp;
        bool smoking = !exploded && ratio <= smokeAt;
        bool burning = !exploded && ratio <= fireAt;

        if (smoking && smokeEffects.Count == 0)
            CreateEffects(smokeEffects, true);
        if (burning && fireEffects.Count == 0)
            CreateEffects(fireEffects, false);

        foreach (ParticleSystem smoke in smokeEffects)
            SetEmitting(smoke, smoking);
        foreach (ParticleSystem fire in fireEffects)
            SetEmitting(fire, burning);

        if (burning)
            takeHp(burnDamagePerSecond * Time.deltaTime);
    }

   
    void CreateEffects(List<ParticleSystem> effects, bool smoke)
    {
        if (smokePoints != null)
        {
            foreach (Transform point in smokePoints)
            {
                if (point != null)
                    effects.Add(smoke
                        ? ExplosionEffect.CreateSmokeEmitter(point, Vector3.zero)
                        : ExplosionEffect.CreateFireEmitter(point, Vector3.zero));
            }
        }

        if (effects.Count == 0)
            effects.Add(smoke
                ? ExplosionEffect.CreateSmokeEmitter(transform, damageEffectOffset)
                : ExplosionEffect.CreateFireEmitter(transform, damageEffectOffset));
    }

    static void SetEmitting(ParticleSystem effect, bool on)
    {
        if (effect == null)
            return;
        if (on && !effect.isEmitting)
            effect.Play();
        else if (!on && effect.isEmitting)
            effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void Explode()
    {
        exploded = true;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && destroyCarSprite != null)
            spriteRenderer.sprite = destroyCarSprite;

        if (playerInCar)
            ExitCar();
        UnloadAll();

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        Vector2 center = transform.position;
        GameObject attacker = lastAttacker != null ? lastAttacker : gameObject;

        ExplosionEffect.Spawn(center, explosionRadius, explosionEffectPrefab);
        Sound.Play(explosionSound, center, 1f, 0.05f);
        CameraShake.Shake(center, shakeStrength, shakeDuration);
        Danger.Report(center, explosionRadius * 4f, attacker);
        DamageArea(center, attacker);
    }

    void DamageArea(Vector2 center, GameObject attacker)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius);
        HashSet<Object> damaged = new HashSet<Object>();

        foreach (Collider2D hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
                continue;

            float distance = Vector2.Distance(center, hit.ClosestPoint(center));
            float falloff = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(distance / explosionRadius));
            float damage = explosionDamage * falloff;

            NPC npc = hit.GetComponentInParent<NPC>();
            if (npc != null && damaged.Add(npc))
                npc.TakeDamage(damage, attacker);

            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && damaged.Add(playerHealth))
                playerHealth.TakeDamage(damage);

            Car car = hit.GetComponentInParent<Car>();
            if (car != null && car != this && damaged.Add(car))
                car.takeHp(damage, attacker);

            Rigidbody2D body = hit.attachedRigidbody;
            if (body != null && body != rb && body.bodyType == RigidbodyType2D.Dynamic && damaged.Add(body))
            {
                Vector2 push = body.position - center;
                if (push.sqrMagnitude < 0.001f)
                    push = Random.insideUnitCircle;
                body.AddForce(push.normalized * explosionForce * falloff, ForceMode2D.Impulse);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        if (smokePoints == null)
            return;
        Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.9f);
        foreach (Transform point in smokePoints)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.25f);
        }
    }

    public void Landing()
    {
        Landing(null);
    }

    // who — тот, кто садится (игрок). Если за рулём NPC — это угон.
    public void Landing(GameObject who)
    {
        if (who != null)
            player = who;
        if (player == null)
            player = GameObject.FindWithTag("Player");

        if (!playerInCar && hp > 0 && player != null)
        {
            if (driver != null)
            {
                NPC victim = driver;
                List<NPC> others = new List<NPC>(passengers);
                UnloadAll();
                victim.OnCarjacked(this, player);
                foreach (NPC npc in others)
                {
                    if (npc != null)
                        npc.OnCarjacked(this, player);
                }
                WantedLevel.Instance.RaiseTo(isPolice ? 2 : 1);
            }
            else if (isPolice)
                WantedLevel.Instance.RaiseTo(2);

            player.SetActive(false);
            playerInCar = true;
            Occupied = this;
            enteredFrame = Time.frameCount;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            currentSteer = 0f;
            Debug.Log("Сел в машину");
            cm.Target.TrackingTarget = gameObject.transform;
        }
    }

    void ExitCar()
    {
        currentSteer = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        player.transform.position = transform.position + transform.right * exitOffset;
        player.transform.rotation = Quaternion.identity;

        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
        }

        player.SetActive(true);
        playerInCar = false;
        restoringCamera = true;
        if (Occupied == this)
            Occupied = null;
        throttle = 0f;
        steer = 0f;
        Debug.Log("Вышел из машины");
        cm.Target.TrackingTarget = player.transform;
    }
}
