using UnityEngine;

public class Legs : MonoBehaviour
{
    public PlayerController pc;
    public Animator anim;
    [Tooltip("Скорость поворота ног, градусов в секунду. 0 — поворот мгновенный.")]
    public float rotateSpeed = 900f;
    [Tooltip("Поправка, если спрайт ног нарисован не вправо (например, вверх — то -90).")]
    public float angleOffset = 0f;

    float legsAngle;

    void Start()
    {
        anim = GetComponent<Animator>();
        if (pc == null)
            pc = GetComponentInParent<PlayerController>();
        legsAngle = transform.eulerAngles.z;
    }

    void Update()
    {
        SwitchMove();
    }

 
   

    void SwitchMove()
    {
        if (anim == null || pc == null)
            return;

        anim.SetBool("isMove", pc.moveVector.x != 0 || pc.moveVector.y != 0);
    }
}
