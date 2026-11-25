using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    public float speed;

    public  Vector2 moveVector;
    Vector3 diference;
    float rotateZ;
    private Animator anim;
    private Inventory inventory;
    [Tooltip("Запасной префаб пули: используется, если у оружия не задан свой Bullet Prefab.")]
    public GameObject bulletPrefab;
    public bool canShot = true;
    public Transform bulletSpawnPoint;
    public float fistDamage = 10f;
    public float fistDistance = 2.2f;
    public float fistRadius = 0.75f;
    public float fistCooldown = 0.23f;
    public float fistKnockback = 12f;
    public float gunshotNoiseRadius = 18f;

    [Header("Звуки")]
    [Tooltip("Звук выстрела, если у оружия не задан свой Shot Sound.")]
    public AudioClip defaultShotSound;
    [Tooltip("Взмах кулаком (для ножа берётся Shot Sound ножа, если задан).")]
    public AudioClip punchSound;
    [Tooltip("Попадание удара по кому-то или чему-то.")]
    public AudioClip punchHitSound;
    [Tooltip("Звуки шагов. Каждый шаг берётся случайный, не повторяя предыдущий.")]
    public AudioClip[] footstepSounds;
    [Tooltip("Через сколько пройденных юнитов звучит следующий шаг.")]
    public float stepDistance = 1.5f;
    public float footstepVolume = 0.5f;

    [Header("Разброс")]
    [Tooltip("Сколько градусов разброса добавляет каждый выстрел.")]
    public float spreadPerShot = 2.5f;
    public float maxExtraSpread = 14f;
    [Tooltip("Сколько градусов в секунду разброс уменьшается, когда не стреляешь.")]
    public float spreadRecovery = 18f;
    public float moveSpreadMultiplier = 1.6f;
    [Tooltip("Во сколько раз больше разброс при стрельбе без прицеливания.")]
    public float hipFireSpreadMultiplier = 2.5f;

    [Header("Прицеливание (ПКМ)")]
    public float aimSpreadMultiplier = 0.35f;
    public float aimMoveSpeedMultiplier = 0.5f;
    [Tooltip("На сколько камера смещается в сторону курсора при прицеливании.")]
    public float aimLookAhead = 4f;

    bool canPunch = true;
    float extraSpread;
    bool isMoving;
    float stepTravel;
    int lastStepIndex = -1;

    public bool IsAiming { get; private set; }

    // Направление движения в мире (уже с учётом поворота к мышке).
    public Vector2 MoveDirection { get; private set; }

    public float CurrentSpread(Item weapon)
    {
        if (weapon == null)
            return 0f;

        float spread = weapon.GetSpread() + extraSpread;
        if (isMoving)
            spread *= moveSpreadMultiplier;
        spread *= IsAiming ? aimSpreadMultiplier : hipFireSpreadMultiplier;
        return spread;
    }

    void Start()
    {
        inventory = GetComponent<Inventory>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        diference = Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position;
        rotateZ = Mathf.Atan2(diference.y, diference.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, rotateZ);
        moveVector.x = Input.GetAxis("Horizontal");
        moveVector.y = Input.GetAxis("Vertical");

        Item equipped = inventory.GetEquippedWeapon();
        IsAiming = Input.GetMouseButton(1) && equipped != null && !equipped.IsMelee;
        float moveSpeed = IsAiming ? speed * aimMoveSpeedMultiplier : speed;

        MoveDirection = Vector2.ClampMagnitude(moveVector, 1f);
        rb.MovePosition(rb.position + MoveDirection * moveSpeed);
        isMoving = moveVector.x != 0 || moveVector.y != 0;
        anim.SetBool("isMove", isMoving);
        UpdateFootsteps((MoveDirection * moveSpeed).magnitude);

        extraSpread = Mathf.MoveTowards(extraSpread, 0f, spreadRecovery * Time.deltaTime);
        CameraAim.SetOffset(IsAiming ? Vector2.ClampMagnitude((Vector2)diference * 0.4f, aimLookAhead) : Vector2.zero);

        Attack();
        UpdateArmedAnimation();
    }

    void OnDisable()
    {
        IsAiming = false;
        canShot = true;
        canPunch = true;
        CameraAim.SetOffset(Vector2.zero);
    }

    void UpdateFootsteps(float travelled)
    {
        if (!isMoving || footstepSounds == null || footstepSounds.Length == 0)
        {
            // Первый шаг после остановки звучит почти сразу.
            stepTravel = stepDistance * 0.7f;
            return;
        }

        stepTravel += travelled;
        if (stepTravel < stepDistance)
            return;
        stepTravel = 0f;

        int index = Random.Range(0, footstepSounds.Length);
        if (footstepSounds.Length > 1 && index == lastStepIndex)
            index = (index + 1) % footstepSounds.Length;
        lastStepIndex = index;

        Sound.Play(footstepSounds[index], transform.position, footstepVolume, 0.1f);
    }

    void UpdateArmedAnimation()
    {
        switch (inventory.inArm)
        {
            case "fist":
                anim.SetInteger("inArm", 0);
                break;
            case "pistol":
                anim.SetInteger("inArm", 1);
                break;
            case "subMachine":
                anim.SetInteger("inArm", 2);
                break;
            case "shotGun":
                anim.SetInteger("inArm", 3);
                break;
            case "automatic":
                anim.SetInteger("inArm", 4);
                break;
        }
    }

    void Attack()
    {
        Item weapon = inventory.GetEquippedWeapon();
        bool melee = weapon == null || weapon.IsMelee;

        if (melee)
        {
            if (Input.GetMouseButton(0) && canPunch)
                StartCoroutine(Punch(weapon));
            else if (canPunch)
                anim.SetBool("isAttack", false);
            return;
        }

        if (!canShot || inventory.IsReloading || BulletPrefabFor(weapon) == null || bulletSpawnPoint == null)
            return;
        if (weapon.patron_Counter <= 0)
            return;

        bool fire = weapon.IsAutomatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (!fire)
            return;

        StartCoroutine(FireWeapon(weapon));
    }

    IEnumerator Punch(Item weapon)
    {
        canPunch = false;
        anim.SetBool("isAttack", true);
        float damage = weapon != null ? weapon.GetDamage() : fistDamage;
        float cooldown = weapon != null ? weapon.GetFireDelay() : fistCooldown;
        bool hitSomething = HitWithFist(damage);
        AudioClip swing = weapon != null && weapon.shotSound != null ? weapon.shotSound : punchSound;
        Sound.Play(swing, transform.position);
        if (hitSomething)
            Sound.Play(punchHitSound, transform.position);
        yield return new WaitForSeconds(cooldown);
        canPunch = true;
        if (inventory.inArm != "fist" || !Input.GetMouseButton(0))
            anim.SetBool("isAttack", false);
    }

    bool HitWithFist(float damage)
    {
        Vector2 origin = (Vector2)transform.position + (Vector2)transform.right * fistDistance;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, fistRadius);
        bool hitSomething = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit.isTrigger || hit.attachedRigidbody == rb)
                continue;
            hitSomething = true;

            Car car = hit.GetComponentInParent<Car>();
            if (car != null)
                car.takeHp(damage, gameObject);

            NPC npc = hit.GetComponentInParent<NPC>();
            if (npc != null)
                npc.TakeDamage(damage, gameObject);

            Rigidbody2D hitBody = hit.attachedRigidbody;
            if (hitBody != null && hitBody.bodyType == RigidbodyType2D.Dynamic)
            {
                Vector2 push = hitBody.position - rb.position;
                if (push.sqrMagnitude < 0.001f)
                    push = transform.right;
                hitBody.AddForce(push.normalized * fistKnockback, ForceMode2D.Impulse);
            }
        }
        return hitSomething;
    }

    IEnumerator FireWeapon(Item weapon)
    {
        canShot = false;
        int pellets = weapon.GetPelletCount();
        float spread = CurrentSpread(weapon);
        float damage = weapon.GetDamage();
        GameObject prefab = BulletPrefabFor(weapon);

        for (int i = 0; i < pellets; i++)
            SpawnBullet(prefab, spread, damage);

        extraSpread = Mathf.Min(maxExtraSpread, extraSpread + spreadPerShot);
        Sound.Play(weapon.shotSound != null ? weapon.shotSound : defaultShotSound, transform.position);

        Danger.Report(transform.position, gunshotNoiseRadius, gameObject);
        inventory.TryConsumeAmmo(1);
        yield return new WaitForSeconds(weapon.GetFireDelay());
        canShot = true;
    }

    GameObject BulletPrefabFor(Item weapon)
    {
        if (weapon != null && weapon.bulletPrefab != null)
            return weapon.bulletPrefab;
        return bulletPrefab;
    }

    void SpawnBullet(GameObject prefab, float spread, float damage)
    {
        float angle = Random.Range(-spread, spread);
        Quaternion rotation = bulletSpawnPoint.rotation * Quaternion.Euler(0f, 0f, angle);
        GameObject bulletObject = Instantiate(prefab, bulletSpawnPoint.position, rotation);
        Bullet bullet = bulletObject.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.damage = damage;
            bullet.SetOwner(gameObject);
        }
    }

    public IEnumerator RateOfFire(float timer)
    {
        Instantiate(bulletPrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        canShot = false;
        yield return new WaitForSeconds(timer);
        canShot = true;
    }

  
}
