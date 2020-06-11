using UnityEngine;

public class TimeController : MonoBehaviour
{
    public int timeSecond = 40;
    private float intervalTime = 1;

    // Start is called before the first frame update
    void Start()
    {
        GetComponent<UnityEngine.UI.Text>().text = "";
    }

    public void SetTime(int time)
    {
        timeSecond = time;
    }

    // Update is called once per frame
    void Update()
    {
        // 倒计时
        if (timeSecond > 0)
        {
            intervalTime -= Time.deltaTime;
            if (intervalTime <= 0)
            {
                intervalTime++;
                timeSecond--;

                // 格式化时间
                GetComponent<UnityEngine.UI.Text>().text = string.Format("{0:D2}:{1:D2}",
                    timeSecond / 60, timeSecond % 60
                );
            }
        }
    }
}
