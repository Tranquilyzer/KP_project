using UnityEngine;

public class LandingZone : MonoBehaviour
{
    public Car car;


    private void OnTriggerStay2D(Collider2D collision)
    {
      
        if (collision.CompareTag("Player") && Input.GetKeyDown(KeyCode.F))
        {
            PlayerHealth health = collision.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead)
                return;
            car.Landing(collision.gameObject);
        }
    }
}
