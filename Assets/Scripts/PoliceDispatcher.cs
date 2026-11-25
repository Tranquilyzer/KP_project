using System.Collections.Generic;
using UnityEngine;

// Пока есть розыск, присылает полицейские машины. Машина появляется за экраном,
// едет к игроку (CarAI), тормозит рядом, и полицейские выходят и продолжают погоню пешком.
// Когда розыск снят, лишние машины и полицейские убираются, как только окажутся за экраном.
public class PoliceDispatcher : MonoBehaviour
{
    [Tooltip("Префаб полицейской машины (компонент Car). Галочка Is Police ставится сама.")]
    public GameObject policeCarPrefab;
    [Tooltip("Префаб полицейского (NPC с типом police).")]
    public GameObject officerPrefab;
    public int officersPerCar = 2;

    [Tooltip("Сколько машин на каждую звезду розыска.")]
    public int carsPerStar = 1;
    public int maxCars = 4;
    [Tooltip("Раз во сколько секунд может приехать новая машина.")]
    public float dispatchInterval = 8f;
    [Tooltip("С какого уровня розыска присылать машины.")]
    public int minWantedLevel = 1;
    public Vector2 spawnDistance = new Vector2(25f, 45f);

    readonly List<Car> cars = new List<Car>();
    readonly List<NPC> officers = new List<NPC>();
    float nextDispatch;

    WantedLevel Wanted => WantedLevel.Instance;

    void Update()
    {
        cars.RemoveAll(c => c == null);
        officers.RemoveAll(o => o == null);

        if (Wanted.level < minWantedLevel)
        {
            CleanUp();
            return;
        }

        if (Time.time < nextDispatch)
            return;
        nextDispatch = Time.time + dispatchInterval;

        int activeCars = 0;
        foreach (Car car in cars)
        {
            if (!car.IsWrecked)
                activeCars++;
        }

        int allowed = Mathf.Min(maxCars, Wanted.level * carsPerStar);
        if (activeCars < allowed)
            Dispatch();
    }

    void Dispatch()
    {
        Transform player = Wanted.PlayerTarget;
        if (policeCarPrefab == null || player == null || !TryFindSpawn(player.position, out Vector2 position))
            return;

        // Машина смотрит в сторону игрока (у машины перед — transform.up).
        Vector2 toPlayer = (Vector2)player.position - position;
        float angle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;

        Car car = Instantiate(policeCarPrefab, position, Quaternion.Euler(0f, 0f, angle)).GetComponent<Car>();
        if (car == null)
            return;
        car.isPolice = true;
        car.driverPrefab = null;
        cars.Add(car);

        for (int i = 0; i < officersPerCar && officerPrefab != null; i++)
        {
            NPC officer = Instantiate(officerPrefab, position, Quaternion.identity).GetComponent<NPC>();
            if (officer == null)
                continue;
            officer.type = NPC.NpcType.police;
            officers.Add(officer);

            if (i == 0)
                car.EnterAsDriver(officer);
            else
                car.AddPassenger(officer);
        }
    }

    bool TryFindSpawn(Vector2 around, out Vector2 position)
    {
        position = Vector2.zero;
        Grid2D grid = Grid2D.Instance;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 candidate;
            if (grid != null && grid.Grid != null)
            {
                Node2D node = grid.Grid[Random.Range(0, grid.gridSizeX), Random.Range(0, grid.gridSizeY)];
                if (!grid.IsPassable(node))
                    continue;
                candidate = node.worldPosition;
            }
            else
                candidate = around + Random.insideUnitCircle.normalized * Random.Range(spawnDistance.x, spawnDistance.y);

            float distance = Vector2.Distance(around, candidate);
            if (distance < spawnDistance.x || distance > spawnDistance.y || IsOnScreen(candidate))
                continue;
            if (IsBlocked(candidate))
                continue;

            position = candidate;
            return true;
        }
        return false;
    }

    static bool IsBlocked(Vector2 position)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, 2f))
        {
            if (!hit.isTrigger)
                return true;
        }
        return false;
    }

    // Розыска нет — убираем присланную полицию, когда её не видно.
    void CleanUp()
    {
        Transform player = Wanted.PlayerTarget;
        for (int i = officers.Count - 1; i >= 0; i--)
        {
            NPC officer = officers[i];
            if (officer.gameObject.activeInHierarchy && CanRemove(officer.transform.position, player))
            {
                Destroy(officer.gameObject);
                officers.RemoveAt(i);
            }
        }

        for (int i = cars.Count - 1; i >= 0; i--)
        {
            Car car = cars[i];
            if (car.playerInCar || !CanRemove(car.transform.position, player))
                continue;

            if (car.driver != null)
                Destroy(car.driver.gameObject);
            foreach (NPC passenger in car.passengers)
            {
                if (passenger != null)
                    Destroy(passenger.gameObject);
            }
            Destroy(car.gameObject);
            cars.RemoveAt(i);
        }
    }

    static bool CanRemove(Vector2 position, Transform player)
    {
        if (IsOnScreen(position))
            return false;
        return player == null || Vector2.Distance(position, player.position) > 25f;
    }

    static bool IsOnScreen(Vector2 position)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 viewport = cam.WorldToViewportPoint(position);
        return viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
    }
}
