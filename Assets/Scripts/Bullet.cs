using Unity.VisualScripting;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float timeDestroy = 3f;
    public float speed = 20f;
    public float damage = 20f;
    public Rigidbody2D rb;
    public GameObject owner;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = transform.right * speed;
        gameObject.transform.Rotate(0, 0, -90, 0);
        Invoke("DestroyBullet", timeDestroy);
    }

    public void SetOwner(GameObject shooter)
    {
        owner = shooter;
        if (shooter == null)
            return;

        Collider2D[] bulletColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] shooterColliders = shooter.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D bulletCollider in bulletColliders)
        {
            foreach (Collider2D shooterCollider in shooterColliders)
                Physics2D.IgnoreCollision(bulletCollider, shooterCollider);
        }
    }

    void DestroyBullet()
    {
        Destroy(this.gameObject);
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        GameObject hit = collision.gameObject;
        if (hit.GetComponent<Bullet>() != null)
            return;

        if (hit.CompareTag("Car"))
        {
            hit.GetComponent<Car>().takeHp(damage, owner);
        }
        else
        {
            NPC npc = hit.GetComponentInParent<NPC>();
            if (npc != null)
                npc.TakeDamage(damage, owner);

            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(damage);
        }

        DestroyBullet();
    }

}
