using UnityEngine;

public class RandomEventTester : MonoBehaviour
{
    public SimpleLauncher launcher;

    public bool autoRun = false;
    public float interval = 2f;

    private float nextEventTime = 0f;

    private readonly string[] chatNicknames =
    {
        "user123",
        "applecat",
        "RangEFan",
        "viewer01",
        "mintchoco"
    };

    private readonly string[] chatMessages =
    {
        "안녕!",
        "오늘 방송 좋아요",
        "이거 멋있다",
        "와 반응 빠르다",
        "재밌다"
    };

    private readonly string[] donationMessages =
    {
        "화이팅!",
        "응원합니다",
        "좋은 방송 감사합니다",
        "이벤트 반응 좋네요",
        "계속 달려주세요"
    };

    void Start()
    {
        nextEventTime = Time.time + interval;
    }

    void Update()
    {
        if (launcher == null)
            return;

        if (InputKeyHelper.GetKeyDown(KeyCode.R))
        {
            TriggerRandomEvent();
        }

        if (InputKeyHelper.GetKeyDown(KeyCode.T))
        {
            autoRun = !autoRun;
            Debug.Log("Auto Run: " + autoRun);
        }

        if (!autoRun)
            return;

        if (Time.time >= nextEventTime)
        {
            TriggerRandomEvent();
            nextEventTime = Time.time + interval;
        }
    }

    void TriggerRandomEvent()
    {
        int randomType = Random.Range(0, 3);

        if (randomType == 0)
        {
            TestEventData data = new TestEventData
            {
                eventType = "Chat",
                nickname = GetRandom(chatNicknames),
                message = GetRandom(chatMessages),
                amount = 0
            };

            launcher.LaunchByLabel("Chat", data);
            Debug.Log($"Random Chat: {data.nickname} / {data.message}");
        }
        else if (randomType == 1)
        {
            TestEventData data = new TestEventData
            {
                eventType = "싱글샷",
                nickname = GetRandom(chatNicknames),
                message = GetRandom(donationMessages),
                amount = 1000
            };

            launcher.LaunchByLabel("싱글샷", data);
            Debug.Log($"Random Single Shot: {data.nickname} / {data.amount} / {data.message}");
        }
        else
        {
            TestEventData data = new TestEventData
            {
                eventType = "샷건",
                nickname = GetRandom(chatNicknames),
                message = GetRandom(donationMessages),
                amount = 5000
            };

            launcher.LaunchByLabel("샷건", data);
            Debug.Log($"Random Shotgun: {data.nickname} / {data.amount} / {data.message}");
        }
    }

    string GetRandom(string[] values)
    {
        if (values == null || values.Length == 0)
            return "";

        int index = Random.Range(0, values.Length);
        return values[index];
    }
}
