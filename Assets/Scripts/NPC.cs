using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NPC : MonoBehaviour
{
    public enum NpcType
    {
        civilian,
        bandit,
        police
    }

    enum State
    {
        Wander,
        Flee,
        Fight,
        Chase,
        Knocked,
        Reclaim,
        Dead
    }

    [Header("Общее")]
    public NpcType type = NpcType.civilian;
    public float hp = 50f;
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float wanderRadius = 6f;
    public float sightRange = 15f;
    public Sprite deadSprite;
   
    public GameObject dropOnDeath;

    [Header("Звуки")]
    public AudioClip shotSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;

    [Header("Сбит машиной")]
    public Sprite knockedSprite;
    public float knockedTime = 2f;

    [Header("Угон")]
    [Tooltip("Шанс, что мирный водитель после угона попытается вернуть машину (иначе убежит).")]
    [Range(0f, 1f)] public float reclaimChance = 0.5f;
    [Tooltip("Сколько секунд он пытается вернуть машину, прежде чем сдаться.")]
    public float reclaimTime = 15f;

    [Header("Реакция на опасность")]
    public float fleeTime = 6f;
   
    [Range(0f, 1f)] public float fightBackChance = 0.6f;
    public float loseTargetTime = 6f;

    [Header("Стрельба")]
   
    public Sprite armedSprite;

    public float armedSpriteTime = 1.5f;
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public float bulletDamage = 10f;
    public float fireDelay = 0.6f;
    public float spread = 6f;
    public float shootRange = 10f;
    public float preferredDistance = 6f;
    public float gunshotNoiseRadius = 12f;

    [Header("Полиция")]
    
    public int shootFromWantedLevel = 2;
    public float meleeDamage = 10f;
    public float meleeRange = 1.5f;
    public float meleeDelay = 1f;

    [Header("Обход препятствий")]
    public float bodyRadius = 0.4f;
    public float avoidDistance = 1.2f;

    [Header("Маршрут")]
  
    public Transform[] route;
  
    public bool routeLoop = true;
    
    public Vector2 routeWaitTime = new Vector2(0.5f, 2f);

    [Header("Поиск пути")]
    
    public float repathInterval = 0.5f;
 
    public float fleeDistance = 10f;
    public float waypointReach = 0.35f;

    static readonly float[] AvoidAngles = { 30f, 60f, 90f, 120f, 150f };

    Rigidbody2D rb;
    Animator anim;
    bool hasMoveParam;
    State state = State.Wander;
    Vector2 homePosition;
    Vector2 wanderTarget;
    float waitTimer;
    float stateTimer;
    Transform target;
    Vector2 threatPosition;
    float lastSeenTargetTime;
    float nextAttackTime;
    State resumeState;
    float knockedUntil;
    Sprite normalSprite;
    float lastHurtSoundTime = -1f;
    Vector2 fleeTarget;
    readonly List<Vector2> path = new List<Vector2>();
    int pathIndex;
    Vector2 pathDestination;
    float nextRepathTime;
    bool hasPath;
    int routeIndex;
    int routeDirection = 1;
    float avoidSide = 1f;
    float stuckTimer;
    Vector2 lastMovePosition;
    Vector2 sidestep;
    float sidestepUntil;
    SpriteRenderer bodyRenderer;
    Sprite defaultSprite;
    float armedUntil;
    bool showingArmed;
    bool started;
    Car reclaimCar;
    float reclaimUntil;

    public bool IsDead => state == State.Dead;


    public float DeathTime { get; private set; }

    WantedLevel Wanted => WantedLevel.Instance;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 4f;

        anim = GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in anim.parameters)
            {
                if (parameter.name == "isMove")
                    hasMoveParam = true;
            }
        }

        bodyRenderer = GetComponent<SpriteRenderer>();
        if (bodyRenderer != null)
            defaultSprite = bodyRenderer.sprite;
    }

   
    void UpdateArmedSprite()
    {
        if (armedSprite == null || bodyRenderer == null)
            return;

        bool armed = Time.time < armedUntil;
        if (armed)
        {
            if (!showingArmed && anim != null)
                anim.enabled = false;
            bodyRenderer.sprite = armedSprite;
        }
        else if (showingArmed)
        {
            bodyRenderer.sprite = defaultSprite;
            if (anim != null)
                anim.enabled = true;
        }
        showingArmed = armed;
    }

    void HideArmedSprite()
    {
        if (!showingArmed)
            return;

        showingArmed = false;
        if (bodyRenderer != null)
            bodyRenderer.sprite = defaultSprite;
    }

    void Start()
    {
        if (!started)
            Init();
    }

    void Init()
    {
        started = true;
        homePosition = rb.position;
        EnterWander();
    }

    void OnEnable()
    {
        Danger.Reported += OnDanger;
    }

    void OnDisable()
    {
        Danger.Reported -= OnDanger;
    }

    void FixedUpdate()
    {
        if (state == State.Dead)
            return;

        if (state == State.Knocked)
        {
            Knocked();
            return;
        }

        UpdateArmedSprite();

        if (type == NpcType.police)
            UpdatePoliceAlert();

        switch (state)
        {
            case State.Wander: Wander(); break;
            case State.Flee: Flee(); break;
            case State.Fight: Fight(); break;
            case State.Chase: Chase(); break;
            case State.Reclaim: Reclaim(); break;
        }
    }


    void EnterWander()
    {
        state = State.Wander;
        target = null;
        ClearPath();

        if (HasRoute)
        {
            // После погони/бегства продолжаем маршрут с ближайшей точки.
            routeIndex = NearestRouteIndex();
            SetRouteTarget(0f);
        }
        else
            PickWanderPoint();
    }

    bool HasRoute
    {
        get
        {
            if (route == null)
                return false;
            foreach (Transform point in route)
            {
                if (point != null)
                    return true;
            }
            return false;
        }
    }

    int NearestRouteIndex()
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < route.Length; i++)
        {
            if (route[i] == null)
                continue;
            float distance = ((Vector2)route[i].position - rb.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    void SetRouteTarget(float wait)
    {
        wanderTarget = route[routeIndex].position;
        waitTimer = wait;
        ClearPath();
        float distance = Vector2.Distance(rb.position, wanderTarget);
        stateTimer = Time.time + wait + distance / Mathf.Max(walkSpeed, 0.01f) * 2f + 5f;
    }

    void AdvanceRoute()
    {
        int count = route.Length;
        for (int attempt = 0; attempt < count; attempt++)
        {
            if (routeLoop || count == 1)
                routeIndex = (routeIndex + 1) % count;
            else
            {
                if (routeIndex + routeDirection >= count || routeIndex + routeDirection < 0)
                    routeDirection = -routeDirection;
                routeIndex += routeDirection;
            }

            if (route[routeIndex] != null)
                break;
        }

        SetRouteTarget(Random.Range(routeWaitTime.x, routeWaitTime.y));
    }

    void OnDrawGizmos()
    {
        if (!HasRoute)
            return;

        Gizmos.color = new Color(1f, 0.85f, 0f, 0.9f);
        Vector3 previous = Vector3.zero;
        Vector3 first = Vector3.zero;
        bool hasPrevious = false;
        foreach (Transform point in route)
        {
            if (point == null)
                continue;
            Gizmos.DrawWireSphere(point.position, 0.3f);
            if (hasPrevious)
                Gizmos.DrawLine(previous, point.position);
            else
                first = point.position;
            previous = point.position;
            hasPrevious = true;
        }
        if (routeLoop && hasPrevious)
            Gizmos.DrawLine(previous, first);
    }

    void StartFlee(Vector2 from)
    {
        threatPosition = from;
        stateTimer = Time.time + fleeTime;
        state = State.Flee;
        PickFleePoint();
    }

    void StartFight(Transform enemy)
    {
        if (enemy == null)
            return;

        target = enemy;
        lastSeenTargetTime = Time.time;
        state = State.Fight;
        ClearPath();
    }

    void PickWanderPoint()
    {
        Grid2D grid = Grid2D.Instance;
        wanderTarget = homePosition + Random.insideUnitCircle * wanderRadius;
        for (int i = 0; i < 10 && grid != null && !grid.IsWalkable(wanderTarget); i++)
            wanderTarget = homePosition + Random.insideUnitCircle * wanderRadius;
        ClearPath();
        waitTimer = Random.Range(1f, 3f);
        float distance = Vector2.Distance(rb.position, wanderTarget);
        stateTimer = Time.time + distance / Mathf.Max(walkSpeed, 0.01f) + 3f;
    }

    void Wander()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            Move(Vector2.zero, 0f);
            return;
        }

        Vector2 toTarget = wanderTarget - rb.position;
        if (HasRoute)
        {
            if (toTarget.magnitude < 0.4f || Time.time > stateTimer)
            {
                AdvanceRoute();
                return;
            }

            MoveTo(wanderTarget, walkSpeed);
            return;
        }

        if (toTarget.magnitude < 0.3f || Time.time > stateTimer)
        {
            PickWanderPoint();
            return;
        }

        MoveTo(wanderTarget, walkSpeed);
    }

    void Flee()
    {
        if (Time.time > stateTimer)
        {
            EnterWander();
            return;
        }

        if (Vector2.Distance(rb.position, fleeTarget) < 0.6f)
            PickFleePoint();
        MoveTo(fleeTarget, runSpeed);
    }


    void PickFleePoint()
    {
        Vector2 away = rb.position - threatPosition;
        if (away.sqrMagnitude < 0.01f)
            away = Random.insideUnitCircle;
        away.Normalize();

        Grid2D grid = Grid2D.Instance;
        fleeTarget = rb.position + away * fleeDistance;
        float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };
        foreach (float angle in angles)
        {
            Vector2 candidate = rb.position + (Vector2)(Quaternion.Euler(0f, 0f, angle) * away) * fleeDistance;
            if (grid == null || grid.IsWalkable(candidate))
            {
                fleeTarget = candidate;
                break;
            }
        }
        ClearPath();
    }

    void Fight()
    {
        if (target != null && Wanted.IsPlayer(target.gameObject))
            target = Wanted.PlayerTarget;

        if (!TargetValid(target))
        {
            EnterWander();
            return;
        }

        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;
        Vector2 direction = distance > 0.01f ? toTarget / distance : (Vector2)transform.right;
        bool sees = distance <= sightRange && HasLineOfSight(target);

        if (sees)
            lastSeenTargetTime = Time.time;
        else if (Time.time - lastSeenTargetTime > loseTargetTime)
        {
            EnterWander();
            return;
        }

        if (!sees || distance > preferredDistance + 1f)
            MoveTo(target.position, runSpeed, sees ? direction : default);
        else if (distance < preferredDistance - 1f)
            Move(-direction, walkSpeed, direction);
        else
            Move(Vector2.zero, 0f, direction);

        if (sees && distance <= shootRange)
            TryShoot(direction);
    }

    void UpdatePoliceAlert()
    {
        if (Wanted.level > 0)
        {
            if (state != State.Chase)
                state = State.Chase;
        }
        else if (state == State.Chase)
            EnterWander();
    }

    void Chase()
    {
        target = Wanted.PlayerTarget;
        if (!TargetValid(target))
        {
            Move(Vector2.zero, 0f);
            return;
        }

        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;
        Vector2 direction = distance > 0.01f ? toTarget / distance : (Vector2)transform.right;
        bool sees = distance <= sightRange && HasLineOfSight(target);
        if (sees)
            Wanted.ReportSeen();

        bool canShoot = bulletPrefab != null && Wanted.level >= shootFromWantedLevel && sees && distance <= shootRange;
        if (canShoot)
        {
            if (distance > preferredDistance)
                MoveTo(target.position, runSpeed, direction);
            else
                Move(Vector2.zero, 0f, direction);
            TryShoot(direction);
            return;
        }

        if (distance > meleeRange)
        {
            MoveTo(target.position, runSpeed, sees ? direction : default);
            return;
        }

        Move(Vector2.zero, 0f, direction);
        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + meleeDelay;
            DealDamage(target, meleeDamage);
        }
    }

    void Knocked()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        if (Time.time < knockedUntil)
            return;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && normalSprite != null)
            spriteRenderer.sprite = normalSprite;
        if (anim != null)
            anim.enabled = true;

        state = resumeState;
        if (state == State.Flee)
            stateTimer = Time.time + fleeTime;
        else if (state == State.Fight)
            lastSeenTargetTime = Time.time;
    }


    // ---------- Машины ----------

    // NPC садится в машину: прячется, пока машина его не высадит.
    public void HideInCar(Car car)
    {
        ClearPath();
        rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(false);
    }

    public void LeaveCar(Vector2 position)
    {
        transform.position = position;
        gameObject.SetActive(true);
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        if (!started)
            Init();
        homePosition = position;
        EnterWander();
    }

    // Игрок выкинул NPC из машины.
    public void OnCarjacked(Car car, GameObject thief)
    {
        switch (type)
        {
            case NpcType.bandit:
                StartFight(Wanted.IsPlayer(thief) ? Wanted.PlayerTarget : thief.transform);
                break;

            case NpcType.police:
                Wanted.RaiseTo(2);
                break;

            default:
                if (Random.value < reclaimChance)
                {
                    reclaimCar = car;
                    reclaimUntil = Time.time + reclaimTime;
                    state = State.Reclaim;
                    ClearPath();
                }
                else
                    StartFlee(car.transform.position);
                break;
        }
    }

    // Испугался (например, машина загорелась). Мирные убегают.
    public void Panic(Vector2 from)
    {
        if (type == NpcType.civilian)
            StartFlee(from);
    }

    // Бежит за угнанной машиной. Если угонщик вышел — садится и уезжает.
    void Reclaim()
    {
        if (reclaimCar == null || reclaimCar.IsWrecked || Time.time > reclaimUntil)
        {
            EnterWander();
            return;
        }

        Vector2 carPosition = reclaimCar.transform.position;
        bool carFree = !reclaimCar.playerInCar && reclaimCar.driver == null;
        if (carFree && Vector2.Distance(rb.position, carPosition) < reclaimCar.exitOffset + 1f)
        {
            reclaimCar.EnterAsDriver(this);
            return;
        }

        MoveTo(carPosition, runSpeed);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        BumpIntoPlayer(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        BumpIntoPlayer(collision);
    }

    // Гуляющий NPC, упёршийся в игрока, останавливается, ждёт и уходит в другую сторону.
    void BumpIntoPlayer(Collision2D collision)
    {
        if (state != State.Wander)
            return;
        if (collision.collider.GetComponentInParent<PlayerController>() == null)
            return;

        Vector2 toPlayer = (Vector2)collision.transform.position - rb.position;
        if (waitTimer <= 0f && Vector2.Dot(toPlayer, wanderTarget - rb.position) <= 0f)
            return; // уже уходит от игрока

        rb.linearVelocity = Vector2.zero;
        if (hasMoveParam)
            anim.SetBool("isMove", false);
        if (waitTimer > 0f)
            return;

        if (HasRoute)
        {
            // На маршруте не сворачиваем — ждём, пока игрок отойдёт.
            waitTimer = Random.Range(1f, 2f);
            return;
        }

        PickWanderPoint();
        Vector2 toTarget = wanderTarget - rb.position;
        if (Vector2.Dot(toPlayer, toTarget) > 0f)
            wanderTarget = rb.position - toTarget;
    }

    public void RunOver(float damage, GameObject car)
    {
        if (state == State.Dead)
            return;

        bool wasKnocked = state == State.Knocked;
        TakeDamage(damage, car);
        if (state == State.Dead)
            return;

        if (!wasKnocked)
        {
            HideArmedSprite();
            resumeState = state;
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                normalSprite = spriteRenderer.sprite;
                if (knockedSprite != null)
                {
                    if (anim != null)
                        anim.enabled = false;
                    spriteRenderer.sprite = knockedSprite;
                }
            }
        }

        state = State.Knocked;
        knockedUntil = Time.time + knockedTime;
    }

    void OnDanger(Vector2 position, float radius, GameObject source)
    {
        if (state == State.Dead || state == State.Knocked || source == gameObject)
            return;
        if ((position - rb.position).sqrMagnitude > radius * radius)
            return;

        bool byPlayer = Wanted.IsPlayer(source);
        NPC sourceNpc = source != null ? source.GetComponent<NPC>() : null;

        switch (type)
        {
            case NpcType.civilian:
                StartFlee(position);
                break;

            case NpcType.bandit:
                if (state == State.Fight)
                    return;
                if (sourceNpc != null && sourceNpc.type == NpcType.bandit)
                    return;
                if (byPlayer && state == State.Wander && Random.value < fightBackChance)
                    StartFight(Wanted.PlayerTarget);
                else
                    StartFlee(position);
                break;

            case NpcType.police:
                if (byPlayer)
                    Wanted.RaiseTo(1);
                break;
        }
    }

    public void TakeDamage(float damage, GameObject attacker)
    {
        if (state == State.Dead || damage <= 0f)
            return;

        bool byPlayer = Wanted.IsPlayer(attacker);
        NPC attackerNpc = attacker != null ? attacker.GetComponent<NPC>() : null;

        hp -= damage;
        if (hp <= 0f)
        {
            Die(attacker, byPlayer);
            return;
        }
    
        if (Time.time - lastHurtSoundTime > 0.1f)
        {
            lastHurtSoundTime = Time.time;
            Sound.Play(hurtSound, transform.position);
        }

        if (byPlayer)
            Wanted.RaiseTo(type == NpcType.police ? 2 : type == NpcType.civilian ? 1 : 0);

        if (state == State.Knocked)
            return;

        switch (type)
        {
            case NpcType.civilian:
                StartFlee(attacker != null ? (Vector2)attacker.transform.position : rb.position);
                break;

            case NpcType.bandit:
                if (attacker != null && (attackerNpc == null || attackerNpc.type != NpcType.bandit))
                    StartFight(byPlayer ? Wanted.PlayerTarget : attacker.transform);
                break;

            case NpcType.police:
                if (attackerNpc != null && attackerNpc.type == NpcType.bandit)
                    StartFight(attacker.transform);
                break;
        }
    }

    void Die(GameObject attacker, bool byPlayer)
    {
        state = State.Dead;
        DeathTime = Time.time;
        hp = 0f;
        Sound.Play(deathSound != null ? deathSound : hurtSound, transform.position);

        if (byPlayer && type != NpcType.bandit)
            Wanted.AddCrime(1);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        if (anim != null)
            anim.enabled = false;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            if (deadSprite != null)
                spriteRenderer.sprite = deadSprite;
            spriteRenderer.sortingOrder -= 1;
        }

        if (dropOnDeath != null)
            Instantiate(dropOnDeath, transform.position, Quaternion.identity);

        Danger.Report(rb.position, 8f, attacker != null ? attacker : gameObject);
    }

  
    void TryShoot(Vector2 direction)
    {
        if (bulletPrefab == null || Time.time < nextAttackTime)
            return;
        nextAttackTime = Time.time + fireDelay;
        armedUntil = Time.time + armedSpriteTime;
        UpdateArmedSprite();

        Vector2 origin = bulletSpawnPoint != null
            ? (Vector2)bulletSpawnPoint.position
            : rb.position + direction * (bodyRadius + 0.4f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + Random.Range(-spread, spread);

        GameObject bulletObject = Instantiate(bulletPrefab, origin, Quaternion.Euler(0f, 0f, angle));
        Bullet bullet = bulletObject.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.damage = bulletDamage;
            bullet.SetOwner(gameObject);
        }

        Sound.Play(shotSound, rb.position);
        Danger.Report(rb.position, gunshotNoiseRadius, gameObject);
    }

    void DealDamage(Transform victim, float damage)
    {
        PlayerHealth playerHealth = victim.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.TakeDamage(damage);

        Car car = victim.GetComponent<Car>();
        if (car != null)
            car.takeHp(damage, gameObject);

        NPC npc = victim.GetComponent<NPC>();
        if (npc != null)
            npc.TakeDamage(damage, gameObject);
    }

    bool TargetValid(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy)
            return false;

        PlayerHealth playerHealth = t.GetComponent<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
            return false;

        NPC npc = t.GetComponent<NPC>();
        return npc == null || !npc.IsDead;
    }

    bool HasLineOfSight(Transform t)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(rb.position, t.position);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.isTrigger || hit.transform.IsChildOf(transform))
                continue;
            if (hit.collider.GetComponent<Bullet>() != null)
                continue;
            return hit.transform == t || hit.transform.IsChildOf(t);
        }
        return true;
    }

    void ClearPath()
    {
        hasPath = false;
        path.Clear();
        pathIndex = 0;
        nextRepathTime = 0f;
    }

   
    void MoveTo(Vector2 destination, float speed, Vector2 look = default)
    {
        Vector2 toDestination = destination - rb.position;
        Grid2D grid = Grid2D.Instance;
        if (grid == null || grid.Grid == null)
        {
            Move(toDestination.normalized, speed, look);
            return;
        }

        // Цель видна по прямой — путь не нужен.
        if (grid.IsLineWalkable(rb.position, destination))
        {
            Move(toDestination.normalized, speed, look);
            return;
        }

        float repathDistance = Mathf.Max(grid.CellWorldSize.x, grid.CellWorldSize.y);
        bool destinationMoved = (destination - pathDestination).sqrMagnitude > repathDistance * repathDistance;
        if (!hasPath || Time.time >= nextRepathTime || destinationMoved)
            BuildPath(grid, destination);

        if (path.Count == 0)
        {
            Move(toDestination.normalized, speed, look);
            return;
        }

        // Срезаем лишние точки: если до следующей точки можно дойти по прямой, идём сразу к ней.
        while (pathIndex < path.Count - 1 &&
               ((path[pathIndex] - rb.position).sqrMagnitude < waypointReach * waypointReach ||
                grid.IsLineWalkable(rb.position, path[pathIndex + 1])))
            pathIndex++;

        Vector2 toWaypoint = path[pathIndex] - rb.position;
        if (pathIndex == path.Count - 1 && toWaypoint.sqrMagnitude < waypointReach * waypointReach)
        {
            // Конец пути. Если цель стоит вне дороги, последний отрезок идём напрямую.
            if (toDestination.sqrMagnitude > waypointReach * waypointReach)
                Move(toDestination.normalized, speed, look);
            else
                Move(Vector2.zero, 0f, look);
            return;
        }

        Move(toWaypoint.normalized, speed, look);
    }

    void BuildPath(Grid2D grid, Vector2 destination)
    {
        hasPath = true;
        pathDestination = destination;
        nextRepathTime = Time.time + repathInterval + Random.Range(0f, 0.2f);
        path.Clear();
        pathIndex = 0;

        List<Node2D> nodes = Pathfinding2D.FindPath(grid, rb.position, destination);
        if (nodes == null)
            return;

        foreach (Node2D node in nodes)
            path.Add(node.worldPosition);
        if (grid.IsWalkable(destination) && path.Count > 0)
            path[path.Count - 1] = destination;
    }

    void Move(Vector2 direction, float speed, Vector2 look = default)
    {
        bool wantsToMove = direction.sqrMagnitude > 0.001f && speed > 0f;
        if (wantsToMove)
        {
            direction = UpdateStuck(direction, speed);
            direction = AvoidObstacles(direction);
        }
        else
            stuckTimer = 0f;

        Vector2 velocity = wantsToMove ? direction * speed : Vector2.zero;
        rb.linearVelocity = velocity;

        Vector2 face = look.sqrMagnitude > 0.001f ? look : velocity;
        if (face.sqrMagnitude > 0.001f)
        {
            rb.angularVelocity = 0f;
            rb.MoveRotation(Mathf.Atan2(face.y, face.x) * Mathf.Rad2Deg);
        }

        if (hasMoveParam)
            anim.SetBool("isMove", velocity.sqrMagnitude > 0.01f);
    }

    Vector2 UpdateStuck(Vector2 direction, float speed)
    {
        float moved = (rb.position - lastMovePosition).magnitude;
        lastMovePosition = rb.position;

        if (moved < speed * Time.fixedDeltaTime * 0.25f)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        if (stuckTimer > 0.6f)
        {
            stuckTimer = 0f;
            avoidSide = -avoidSide;
            sidestep = new Vector2(-direction.y, direction.x) * avoidSide;
            sidestepUntil = Time.time + 0.5f;
            nextRepathTime = 0f;
        }

        if (Time.time < sidestepUntil)
            return (direction * 0.3f + sidestep).normalized;
        return direction;
    }

    Vector2 AvoidObstacles(Vector2 direction)
    {
        if (!IsBlocked(direction))
            return direction;

        foreach (float angle in AvoidAngles)
        {
            Vector2 preferred = Quaternion.Euler(0f, 0f, angle * avoidSide) * direction;
            if (!IsBlocked(preferred))
                return preferred;

            Vector2 other = Quaternion.Euler(0f, 0f, -angle * avoidSide) * direction;
            if (!IsBlocked(other))
            {
                avoidSide = -avoidSide;
                return other;
            }
        }
        return direction;
    }

    bool IsBlocked(Vector2 direction)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(rb.position, bodyRadius, direction, avoidDistance);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.isTrigger || hit.transform.IsChildOf(transform))
                continue;
            if (target != null && hit.transform.IsChildOf(target))
                continue;
            if (hit.collider.GetComponent<Bullet>() != null)
                continue;
            return true;
        }
        return false;
    }
}
