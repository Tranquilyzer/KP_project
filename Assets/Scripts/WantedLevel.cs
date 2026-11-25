using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WantedLevel : MonoBehaviour
{
    public const int MaxLevel = 5;

    public int level;
    [Tooltip("Сколько секунд полиция должна не видеть игрока, чтобы снять одну звезду.")]
    public float loseTime = 12f;
    public TMP_Text wantedText;
    public Image[] starImages;

    static WantedLevel instance;
    GameObject player;
    float unseenTimer;

    public static WantedLevel Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<WantedLevel>();
                if (instance == null)
                    instance = new GameObject("WantedLevel").AddComponent<WantedLevel>();
            }
            return instance;
        }
    }

    // Игрок или машина, в которой он сидит.
    public Transform PlayerTarget
    {
        get
        {
            if (Car.Occupied != null)
                return Car.Occupied.transform;
            return player != null ? player.transform : null;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        player = GameObject.FindWithTag("Player");
        RefreshHud();
    }

    void Update()
    {
        if (level <= 0)
            return;

        unseenTimer += Time.deltaTime;
        if (unseenTimer >= loseTime)
        {
            unseenTimer = 0f;
            SetLevel(level - 1);
        }
    }

    public bool IsPlayer(GameObject obj)
    {
        if (obj == null)
            return false;
        if (obj == player)
            return true;
        return Car.Occupied != null && obj == Car.Occupied.gameObject;
    }

    public void AddCrime(int stars)
    {
        unseenTimer = 0f;
        SetLevel(level + stars);
    }

    public void RaiseTo(int minLevel)
    {
        unseenTimer = 0f;
        if (level < minLevel)
            SetLevel(minLevel);
    }

    public void Clear()
    {
        unseenTimer = 0f;
        SetLevel(0);
    }

    public void ReportSeen()
    {
        unseenTimer = 0f;
    }

    void SetLevel(int value)
    {
        level = Mathf.Clamp(value, 0, MaxLevel);
        RefreshHud();
    }

    void RefreshHud()
    {
        if (wantedText == null)
        {
            GameObject textObject = GameObject.Find("WantedText");
            if (textObject != null)
                wantedText = textObject.GetComponent<TMP_Text>();
        }

        if (wantedText != null)
            wantedText.text = new string('*', level);

        if (starImages == null)
            return;
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] != null)
                starImages[i].enabled = i < level;
        }
    }
}
