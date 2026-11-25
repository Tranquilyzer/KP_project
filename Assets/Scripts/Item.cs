using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class Item : MonoBehaviour
{
    [FormerlySerializedAs("name")]
    public string itemName;
    public int id = 0;
    public int patron_Counter;
    public int capacity;
    public int max_Patrons;
    public float fireRate;
    public float damage;
    public int pelletCount;
    public float spread;
    public int cashAmount = 10;
    public float healAmount = 25f;
    public float armorAmount = 50f;
    public ItemType type;
    [Tooltip("Префаб пули этого оружия.")]
    public GameObject bulletPrefab;
    [Tooltip("Звук выстрела (или удара для холодного оружия).")]
    public AudioClip shotSound;
    public AudioClip reloadSound;
    [Tooltip("Звук подбора. Если пусто — берётся звук для этого типа предмета из Inventory игрока.")]
    public AudioClip pickupSound;
    public Item prefab;
    public TMP_Text counterText;

    public enum ItemType
    {
        edged,
        pistol,
        submachine_gun,
        shotgun,
        automatic_rifle,
        sniper_rifle,
        hewavy,
        cash,
        medkit,
        armor
    }

    public bool IsMelee => type == ItemType.edged;

    public bool IsCash => type == ItemType.cash;

    public bool IsMedkit => type == ItemType.medkit;

    public bool IsArmor => type == ItemType.armor;

    public bool IsAutomatic =>
        type == ItemType.submachine_gun || type == ItemType.automatic_rifle;

    public int SlotIndex
    {
        get
        {
            switch (type)
            {
                case ItemType.edged: return 1;
                case ItemType.pistol: return 2;
                case ItemType.submachine_gun: return 3;
                case ItemType.shotgun: return 4;
                case ItemType.automatic_rifle: return 5;
                case ItemType.sniper_rifle: return 6;
                case ItemType.hewavy: return 7;
                case ItemType.cash: return 8;
                default: return -1;
            }
        }
    }

    public int GetPelletCount()
    {
        if (pelletCount > 0)
            return pelletCount;
        return type == ItemType.shotgun ? 6 : 1;
    }

    public float GetSpread()
    {
        if (spread > 0f)
            return spread;
        return type == ItemType.shotgun ? 14f : 1.5f;
    }

    public float GetDamage()
    {
        if (damage > 0f)
            return damage;

        switch (type)
        {
            case ItemType.edged: return 20f;
            case ItemType.pistol: return 15f;
            case ItemType.submachine_gun: return 10f;
            case ItemType.shotgun: return 8f;
            case ItemType.automatic_rifle: return 12f;
            case ItemType.sniper_rifle: return 40f;
            case ItemType.hewavy: return 50f;
            default: return 10f;
        }
    }

    public float GetFireDelay()
    {
        if (fireRate > 0f && !Mathf.Approximately(fireRate, 1f))
            return fireRate < 1f ? fireRate : 1f / fireRate;

        switch (type)
        {
            case ItemType.edged: return 0.25f;
            case ItemType.pistol: return 0.45f;
            case ItemType.submachine_gun: return 0.08f;
            case ItemType.shotgun: return 0.9f;
            case ItemType.automatic_rifle: return 0.16f;
            case ItemType.sniper_rifle: return 1f;
            case ItemType.hewavy: return 1.15f;
            default: return 0.4f;
        }
    }

    public int AddAmmo(int amount, bool ignoreCapacity = false)
    {
        if (amount <= 0)
            return 0;

        int before = patron_Counter;
        capacity += amount;
        if (!ignoreCapacity && capacity > 0)
            patron_Counter = Mathf.Min(patron_Counter, capacity);
        return patron_Counter - before;
    }
  
}
