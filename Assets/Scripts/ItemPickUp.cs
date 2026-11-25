using UnityEngine;

public class ItemPickUp : MonoBehaviour
{
    public float pickUpDistace;
    public Item item;

    Inventory inventory;
    float ignoreUntil;

    void Awake()
    {
        if (item == null)
            item = GetComponent<Item>();
    }

    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            inventory = player.GetComponent<Inventory>();
    }

    public void IgnorePickup(float seconds)
    {
        ignoreUntil = Time.time + seconds;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        TryPickup(collision);
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        TryPickup(collision);
    }

    void TryPickup(Collider2D collision)
    {
        if (!enabled || Time.time < ignoreUntil)
            return;
        if (!collision.CompareTag("Player"))
            return;

        if (item == null)
            item = GetComponent<Item>();
        if (item == null)
            return;

        if (inventory == null)
            inventory = collision.GetComponent<Inventory>();
        if (inventory == null)
            return;

        bool consumeWorldObject = inventory.TryPickup(item);
        if (consumeWorldObject)
            Destroy(gameObject);
    }
}
