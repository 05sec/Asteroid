using UnityEngine;
using TMPro;

public class SinglePlanet : MonoBehaviour
{
    public Vector3 Position;
    public Camera MainCamera;
    public GameObject CenterCube;
    public GameObject[] spaceShipGroups;
    private GameObject spaceShip;

    // Start is called before the first frame update
    void Start()
    {
        int length = this.spaceShipGroups.Length;
        int index = Random.Range(0, length);
        this.spaceShip = this.spaceShipGroups[index];
        for (int i = 0; i < length; i++)
        {
            if (i != index)
            {
                Destroy(this.spaceShipGroups[i]);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 对象始终面向摄像机
        this.transform.LookAt(MainCamera.transform.position);
        this.spaceShip.transform.LookAt(CenterCube.transform.position);
    }

    public void SetPosition(Vector3 vector)
    {
        this.Position = vector;
    }

    public void SetTeamName(string TeamName)
    {
        TextMeshPro teamNameTexst = (TextMeshPro)GetComponentsInChildren<TextMeshPro>().GetValue(0);
        teamNameTexst.text = TeamName;
    }

    public void SetScoreRank(int Score, int Rank)
    {
        TextMeshPro scoreText = (TextMeshPro)GetComponentsInChildren<TextMeshPro>().GetValue(1);
        scoreText.text = "#" + Rank.ToString() + " / " + Score.ToString();
    }

    public void SetDown(bool Status)
    {
        this.transform.GetChild(2).GetComponent<SpriteRenderer>().enabled = Status;
    }

    public void SetAttack(bool Status)
    {
        this.transform.GetChild(3).GetComponent<SpriteRenderer>().enabled = Status;
    }
}
