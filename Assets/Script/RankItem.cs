using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RankItem : MonoBehaviour
{
    public GameObject teamName;
    public GameObject teamScore;
    public GameObject teamRank;
    public GameObject teamLogo;

    // Use this for initialization
    void Start()
    {
        this.teamLogo.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SetTeamName(string name)
    {
        this.teamName.GetComponent<Text>().text = name;
    }

    public void SetTeamScore(string score)
    {
        this.teamScore.GetComponent<Text>().text = score;
    }

    public void SetTeamRank(string rank)
    {
        this.teamRank.GetComponent<Text>().text = rank;
    }

    public IEnumerator SetTeamLogo(string url)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
            if (www.isNetworkError)
            {
                Debug.Log(www.error);
            }
            else
            {
                byte[] results = www.downloadHandler.data;
                Texture2D texture = new Texture2D(35, 35);
                texture.LoadImage(results);

                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                this.teamLogo.GetComponent<Image>().sprite = sprite;
                this.teamLogo.SetActive(true);

                Resources.UnloadUnusedAssets();
            }
        }
    }

}
