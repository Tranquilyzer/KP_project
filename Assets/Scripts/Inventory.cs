using NUnit.Framework.Internal.Execution;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Experimental.GraphView.Port;

public class Inventory : MonoBehaviour
{
    public string inArm = "fist";
    public int cash;
    public TMP_Text cashText;
    public Image weapoonImage;
    public Sprite[] weaponImageConteyner;
    public Item[] guns_Inventory;
    public TMP_Text counterText;
    public PlayerController playerController;
    public PlayerHealth playerHealth;
    [Tooltip("Звук перезарядки, если у оружия не задан свой Reload Sound.")]
    public AudioClip defaultReloadSound;
    [Tooltip("Время перезарядки, если звук перезарядки не задан.")]
    public float fallbackReloadTime = 1.5f;

    [Header("Звуки подбора")]
    [Tooltip("Если у предмета не задан свой Pickup Sound и нет звука для его типа.")]
    public AudioClip defaultPickupSound;
    public AudioClip weaponPickupSound;
    public AudioClip cashPickupSound;
    public AudioClip medkitPickupSound;
    public AudioClip armorPickupSound;
    public Item EquippedWeapon { get; private set; }

    const int SlotCount = 8;

    void Awake()
    {
        DetachHud();
        playerController  = GetComponent<PlayerController>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (guns_Inventory == null || guns_Inventory.Length < SlotCount)
            guns_Inventory = new Item[SlotCount];

        BindCounterText();
        RefreshCashText();

        if (string.IsNullOrEmpty(inArm))
            EquipFists();
        else
            RefreshHud();
    }

    void Update()
    {
        ReadWeaponHotkeys();
    }

    // HUD лежит внутри игрока, а игрок выключается при посадке в машину.
    // Выносим экранный Canvas в корень сцены, чтобы интерфейс оставался видимым.
    void DetachHud()
    {
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.renderMode != RenderMode.WorldSpace && canvas.transform.parent != null
                && canvas.transform.parent.GetComponentInParent<Canvas>(true) == null)
                canvas.transform.SetParent(null, false);
        }
    }

    void BindCounterText()
    {
        if (counterText != null)
            return;

        GameObject counterObject = GameObject.Find("CounterButtonsText");
        if (counterObject != null)
            counterText = counterObject.GetComponent<TMP_Text>();
    }

    void ReadWeaponHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            EquipMelee();
        if (Input.GetKeyDown(KeyCode.Alpha2))
            TryEquipSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            TryEquipSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha4))
            TryEquipSlot(4);
        if (Input.GetKeyDown(KeyCode.Alpha5))
            TryEquipSlot(5);
        if (Input.GetKeyDown(KeyCode.Alpha6))
            TryEquipSlot(6);
        if (Input.GetKeyDown(KeyCode.Alpha7))
            TryEquipSlot(7);
        if (Input.GetKeyDown(KeyCode.R))
            TryReload();

        ScrollWeapons();
    }

    void ScrollWeapons()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        int direction = scroll > 0f ? -1 : 1;
        int slotsCount = SlotCount - 1;
        int current = EquippedWeapon != null ? EquippedWeapon.SlotIndex : 1;

        for (int step = 1; step <= slotsCount; step++)
        {
            int slot = 1 + ((current - 1 + direction * step) % slotsCount + slotsCount) % slotsCount;
            if (slot == 1)
            {
                EquipMelee();
                return;
            }
            if (TryEquipSlot(slot))
                return;
        }
    }

    public bool TryPickup(Item worldItem)
    {
        if (worldItem == null)
            return false;
        if (playerHealth != null && playerHealth.IsDead)
            return false;

        bool consume = ApplyPickup(worldItem);
        // Подобранное оружие прячется в инвентарь (выключается), остальное уничтожается.
        bool pickedUp = consume || !worldItem.gameObject.activeSelf;
        if (pickedUp)
            Sound.Play(PickupSoundFor(worldItem), transform.position);
        return consume;
    }

    AudioClip PickupSoundFor(Item item)
    {
        if (item.pickupSound != null)
            return item.pickupSound;

        AudioClip byType = null;
        if (item.IsCash)
            byType = cashPickupSound;
        else if (item.IsMedkit)
            byType = medkitPickupSound;
        else if (item.IsArmor)
            byType = armorPickupSound;
        else
            byType = weaponPickupSound;

        return byType != null ? byType : defaultPickupSound;
    }

    bool ApplyPickup(Item worldItem)
    {
        if (worldItem.IsCash)
        {
            AddCash(worldItem.cashAmount);
            return true;
        }

        if (worldItem.IsMedkit)
            return playerHealth != null && playerHealth.Heal(worldItem.healAmount);

        if (worldItem.IsArmor)
            return playerHealth != null && playerHealth.AddArmor(worldItem.armorAmount);

        int slot = worldItem.SlotIndex;
        if (slot < 0 || slot >= guns_Inventory.Length)
            return false;

        Item current = guns_Inventory[slot];
        if (current != null && current != worldItem && current.id == worldItem.id)
        {
            current.AddAmmo(Mathf.Max(0, worldItem.patron_Counter), true);
            EquipSlot(slot);
            return true;
        }

        if (current != null && current != worldItem && current.type == worldItem.type)
        {
            int combinedAmmo = current.patron_Counter + Mathf.Max(0, worldItem.patron_Counter);
            DestroyStashed(current);
            Stash(worldItem);
            worldItem.patron_Counter = combinedAmmo;
            guns_Inventory[slot] = worldItem;
            EquipSlot(slot);
            return false;
        }

        if (current != null && current != worldItem)
            DropWeapon(current, worldItem.transform.position);

        Stash(worldItem);
        guns_Inventory[slot] = worldItem;
        EquipSlot(slot);
        return false;
    }

    public void AddCash(int amount)
    {
        if (amount <= 0)
            return;

        cash += amount;
        RefreshCashText();
    }

    public bool TrySpendCash(int amount)
    {
        if (amount < 0 || cash < amount)
            return false;

        cash -= amount;
        RefreshCashText();
        return true;
    }

    void RefreshCashText()
    {
        if (cashText == null)
        {
            GameObject cashObject = GameObject.Find("CashText");
            if (cashObject != null)
                cashText = cashObject.GetComponent<TMP_Text>();
        }

        if (cashText != null)
        {
            string zeroText ="";
            for (int i = 0; i < 6 - cash.ToString().Length; i++)
                zeroText += "0";
                cashText.text = $"${zeroText}{cash}";
        }
            
    }

    public Item GetEquippedWeapon()
    {
        return EquippedWeapon;
    }

    public bool TryConsumeAmmo(int amount = 1)
    {
        if (EquippedWeapon == null || EquippedWeapon.IsMelee)
            return true;

        if (EquippedWeapon.patron_Counter < amount)
            return false;

        EquippedWeapon.patron_Counter -= amount;
        if (EquippedWeapon.patron_Counter > 0)
            RefreshHud();
        else if (EquippedWeapon.capacity > 0)
        {
            // Магазин пуст, но есть запас — перезаряжаемся автоматически.
            RefreshHud();
            TryReload();
        }
        else
            DiscardEquippedWeapon();

        return true;
    }

    public void EquipFists()
    {
        EquippedWeapon = null;
        inArm = "fist";
        SetHudSprite(0);
        SetAmmoText("","");
    }

    void EquipMelee()
    {
        if (guns_Inventory[1] != null)
            EquipSlot(1);
        else
            EquipFists();
    }

    bool TryEquipSlot(int slot)
    {
        if (slot < 0 || slot >= guns_Inventory.Length || guns_Inventory[slot] == null)
            return false;

        EquipSlot(slot);
        return true;
    }

    void EquipSlot(int slot)
    {
        Item weapon = guns_Inventory[slot];
        if (weapon == null)
        {
            EquipFists();
            return;
        }

        EquippedWeapon = weapon;
        inArm = ArmName(weapon.type);
        SetHudSprite(HudIndex(weapon.type));
        RefreshAmmoText();
    }

    void DiscardEquippedWeapon()
    {
        if (EquippedWeapon == null)
            return;

        int slot = EquippedWeapon.SlotIndex;
        if (slot >= 0 && slot < guns_Inventory.Length && guns_Inventory[slot] == EquippedWeapon)
            guns_Inventory[slot] = null;

        Destroy(EquippedWeapon.gameObject);
        EquipFists();
    }

    void DestroyStashed(Item item)
    {
        int slot = item.SlotIndex;
        if (slot >= 0 && slot < guns_Inventory.Length && guns_Inventory[slot] == item)
            guns_Inventory[slot] = null;

        Destroy(item.gameObject);
    }

    void Stash(Item item)
    {
        ItemPickUp pickup = item.GetComponent<ItemPickUp>();
        if (pickup != null)
            pickup.enabled = false;

        Collider2D[] colliders = item.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody2D body = item.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        item.transform.SetParent(transform, true);
        item.gameObject.SetActive(false);
    }

    void DropWeapon(Item item, Vector3 worldPosition)
    {
        int slot = item.SlotIndex;
        if (slot >= 0 && slot < guns_Inventory.Length && guns_Inventory[slot] == item)
            guns_Inventory[slot] = null;

        item.transform.SetParent(null, true);
        item.transform.position = worldPosition;
        item.gameObject.SetActive(true);

        Rigidbody2D body = item.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        Collider2D[] colliders = item.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = true;

        ItemPickUp pickup = item.GetComponent<ItemPickUp>();
        if (pickup != null)
        {
            pickup.IgnorePickup(0.4f);
            pickup.enabled = true;
        }
    }

    void RefreshHud()
    {
        if (EquippedWeapon == null)
        {
            SetHudSprite(0);
            SetAmmoText("", "");
            return;
        }

        SetHudSprite(HudIndex(EquippedWeapon.type));
        RefreshAmmoText();
    }

    void RefreshAmmoText()
    {
        BindCounterText();
        if (EquippedWeapon == null || EquippedWeapon.IsMelee)
        {
            SetAmmoText("","");
            return;
        }

        SetAmmoText(EquippedWeapon.patron_Counter.ToString(), EquippedWeapon.capacity.ToString());
        if (EquippedWeapon.counterText != null && EquippedWeapon.counterText != counterText)
            EquippedWeapon.counterText.text = EquippedWeapon.patron_Counter.ToString();
    }

    void SetAmmoText(string value , string maxAmmo)
    {
        BindCounterText();
        if (counterText != null)
            counterText.text = $"{value} / {maxAmmo}";
    }

    void SetHudSprite(int index)
    {
        if (weapoonImage == null || weaponImageConteyner == null)
            return;
        if (index < 0 || index >= weaponImageConteyner.Length)
            return;
        weapoonImage.sprite = weaponImageConteyner[index];
    }

    static string ArmName(Item.ItemType type)
    {
        switch (type)
        {
            case Item.ItemType.edged: return "fist";
            case Item.ItemType.pistol: return "pistol";
            case Item.ItemType.submachine_gun: return "subMachine";
            case Item.ItemType.shotgun: return "shotGun";
            case Item.ItemType.automatic_rifle: return "automatic";
            case Item.ItemType.sniper_rifle: return "automatic";
            case Item.ItemType.hewavy: return "shotGun";
            default: return "fist";
        }
    }

    static int HudIndex(Item.ItemType type)
    {
        switch (type)
        {
            case Item.ItemType.edged: return 0;
            case Item.ItemType.pistol: return 1;
            case Item.ItemType.submachine_gun: return 2;
            case Item.ItemType.shotgun: return 3;
            case Item.ItemType.automatic_rifle: return 4;
            case Item.ItemType.sniper_rifle: return 4;
            case Item.ItemType.hewavy: return 5;
            default: return 0;
        }
    }
  
   
    bool isReloading;

    void OnDisable()
    {
        // Корутины останавливаются при выключении игрока (например, сел в машину).
        isReloading = false;
        if (playerController != null)
            playerController.canShot = true;
    }

    public bool IsReloading => isReloading;

    // Ёмкость магазина. Если max_Patrons не задан, магазин вмещает все патроны оружия.
    static int MagazineSize(Item weapon)
    {
        return weapon.max_Patrons > 0 ? weapon.max_Patrons : weapon.patron_Counter + weapon.capacity;
    }

    void TryReload()
    {
        Item weapon = EquippedWeapon;
        if (isReloading || weapon == null || weapon.IsMelee)
            return;
        if (weapon.capacity <= 0 || weapon.patron_Counter >= MagazineSize(weapon))
            return;

        StartCoroutine(ReloadingTimer(weapon));
    }

 public  IEnumerator ReloadingTimer(Item weapon)
    {
        if (weapon == null || weapon.IsMelee || weapon.capacity <= 0)
            yield break;

        isReloading = true;
        playerController.canShot = false;

        // Перезарядка длится столько, сколько звучит звук перезарядки.
        AudioClip clip = weapon.reloadSound != null ? weapon.reloadSound : defaultReloadSound;
        float duration = Sound.Play(clip, transform.position, 1f, 0f);
        if (duration <= 0f)
            duration = fallbackReloadTime;
        yield return new WaitForSeconds(duration);

        playerController.canShot = true;
        isReloading = false;

        if (weapon == null)
            yield break;

        var difference = Mathf.Max(0, MagazineSize(weapon) - weapon.patron_Counter);
        int loaded = Mathf.Min(weapon.capacity, difference);
        weapon.patron_Counter += loaded;
        weapon.capacity -= loaded;
        if (weapon == EquippedWeapon)
            RefreshAmmoText();
    }
}
