using UnityEngine;

// Раскладывает предметы по точкам и возвращает их через respawnDelay секунд
// после того, как предмет подобрали (или унесли/уничтожили).
public class SpawnItems : MonoBehaviour
{
    public GameObject pistol;
    public GameObject submachine;
    public GameObject shotgun;
    public GameObject rifle;
    public GameObject medkit;
    public GameObject armor;


    public Transform pistolPoint;
    public Transform submachinePoint;
    public Transform shotgunPoint;
    public Transform riflePoint;
    public Transform medkitPoint;
    public Transform armorPoint;

    [Tooltip("Через сколько секунд после подбора предмет появляется снова.")]
    public float respawnDelay = 10f;
    [Tooltip("Разложить предметы сразу при старте сцены.")]
    public bool spawnOnStart = true;
    [Tooltip("Если предмет сдвинули дальше этого расстояния от точки, он считается унесённым.")]
    public float pickupDistance = 1.5f;

    class Slot
    {
        public GameObject prefab;
        public Transform point;
        public GameObject current;
        public float respawnAt = -1f;
    }

    Slot[] slots;

    void Start()
    {
        slots = new[]
        {
            new Slot { prefab = pistol, point = pistolPoint },
            new Slot { prefab = submachine, point = submachinePoint },
            new Slot { prefab = shotgun, point = shotgunPoint },
            new Slot { prefab = rifle, point = riflePoint },
            new Slot { prefab = medkit, point = medkitPoint },
            new Slot { prefab = armor, point = armorPoint },
        };

        foreach (Slot slot in slots)
        {
            if (slot.prefab == null || slot.point == null)
                continue;

            if (spawnOnStart)
                Spawn(slot);
            else
                slot.respawnAt = Time.time + respawnDelay;
        }
    }

    void Update()
    {
        foreach (Slot slot in slots)
        {
            if (slot.prefab == null || slot.point == null)
                continue;

            if (IsLyingOnPoint(slot))
            {
                slot.respawnAt = -1f;
                continue;
            }

            // Предмет пропал с точки — запускаем таймер один раз.
            if (slot.respawnAt < 0f)
                slot.respawnAt = Time.time + respawnDelay;
            else if (Time.time >= slot.respawnAt)
                Spawn(slot);
        }
    }

    // Подобранное оружие прячется в игрока (выключается и становится его дочерним объектом),
    // аптечки и деньги уничтожаются — во всех этих случаях предмета на точке больше нет.
    bool IsLyingOnPoint(Slot slot)
    {
        GameObject item = slot.current;
        if (item == null || !item.activeInHierarchy || item.transform.parent != null)
            return false;

        return Vector2.Distance(item.transform.position, slot.point.position) <= pickupDistance;
    }

    void Spawn(Slot slot)
    {
        slot.current = Instantiate(slot.prefab, slot.point.position, slot.point.rotation);
        slot.respawnAt = -1f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        foreach (Transform point in new[] { pistolPoint, submachinePoint, shotgunPoint, riflePoint, medkitPoint, armorPoint })
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.4f);
        }
    }
}
