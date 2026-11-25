using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;


public class PlayerHealth : MonoBehaviour
{
    public float hp = 100f;
    public float maxHp = 100f;
    public float armor;
    public float maxArmor = 100f;
    public Image hpImg;
    public Image armorImg;
    public AudioClip hurtSound;
   
    public AudioClip armorHitSound;
    public AudioClip deathSound;

    [Header("Смерть и возрождение")]
   
    public Transform respawnPoint;
    
    public float respawnDelay = 3f;
    
    public float deathZoomMultiplier = 1.8f;

  
    public TMP_Text deathText;
  
    public Sprite deadSprite;

    // Пока идёт анимация смерти, камерой управляет только она.
    public static bool CameraLocked { get; private set; }

    float lastHurtSoundTime = -1f;
    Vector3 startPosition;

    public bool IsDead => hp <= 0f;

    void Awake()
    {
        startPosition = transform.position;
        RefreshHud();
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || IsDead)
            return;

        float absorbed = Mathf.Min(armor, damage);
        armor -= absorbed;
        hp = Mathf.Max(0f, hp - (damage - absorbed));
        RefreshHud();

        if (IsDead)
        {
            Sound.Play(deathSound != null ? deathSound : hurtSound, transform.position);
            StartCoroutine(DeathRoutine());
        }
        else if (Time.time - lastHurtSoundTime > 0.1f)
        {
            lastHurtSoundTime = Time.time;
            bool onlyArmor = absorbed >= damage && armorHitSound != null;
            Sound.Play(onlyArmor ? armorHitSound : hurtSound, transform.position);
        }
    }

    public bool Heal(float amount)
    {
        if (amount <= 0f || IsDead || hp >= maxHp)
            return false;

        hp = Mathf.Min(maxHp, hp + amount);
        RefreshHud();
        return true;
    }

    public bool AddArmor(float amount)
    {
        if (amount <= 0f || armor >= maxArmor)
            return false;

        armor = Mathf.Min(maxArmor, armor + amount);
        RefreshHud();
        return true;
    }

    void RefreshHud()
    {
        hpImg.fillAmount = hp/100;
        armorImg.fillAmount = armor / 100;


    }

    // ---------- Смерть ----------

    IEnumerator DeathRoutine()
    {
        SetControls(false);

        SpriteRenderer body = GetComponent<SpriteRenderer>();
        Sprite normalSprite = body != null ? body.sprite : null;
        Animator anim = GetComponent<Animator>();
        if (deadSprite != null && body != null)
        {
            if (anim != null)
                anim.enabled = false;
            body.sprite = deadSprite;
        }

      

   
        CinemachineCamera cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        float baseSize = cinemachineCamera != null ? cinemachineCamera.Lens.OrthographicSize : 0f;
        CameraLocked = true;

        float time = 0f;
        while (time < respawnDelay)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / respawnDelay);
            if (cinemachineCamera != null)
                cinemachineCamera.Lens.OrthographicSize = Mathf.Lerp(baseSize, baseSize * deathZoomMultiplier, Mathf.SmoothStep(0f, 1f, k));
          deathText.gameObject.SetActive(true);
            yield return null;
        }

        // Возрождение.
        deathText.gameObject.SetActive(false);
        transform.position = respawnPoint != null ? respawnPoint.position : startPosition;
        transform.rotation = Quaternion.identity;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        hp = maxHp;
        armor = 0f;
        RefreshHud();

        if (body != null && normalSprite != null)
            body.sprite = normalSprite;
        if (anim != null)
            anim.enabled = true;

        if (cinemachineCamera != null)
            cinemachineCamera.Lens.OrthographicSize = baseSize;
        CameraLocked = false;


        WantedLevel.Instance.Clear();
        SetControls(true);
    }

    void OnDisable()
    {
        CameraLocked = false;
    }

    void SetControls(bool enabled)
    {
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            if (!enabled)
                controller.moveVector = Vector2.zero;
            controller.enabled = enabled;
        }

        Inventory inventory = GetComponent<Inventory>();
        if (inventory != null)
            inventory.enabled = enabled;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }


}
