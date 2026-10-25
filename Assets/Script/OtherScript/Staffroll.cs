using UnityEngine;
using UnityEngine.UI;

public class Staffroll : MonoBehaviour
{
    [SerializeField] private Image BackGround;
    [SerializeField] private float Speed = 30f;
    private float time = 0;
    void Start()
    {
        StartCoroutine(RandomBGM());
        AudioManager.Instance.PlayBGM(AudioManager.BGMState.Battle);
    }
    private void OnDestroy()
    {
        BackGround = null;
        time = 0;
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Cancel"))
        {
            GameManager.Instance.ChangeScene("GameMenuScene");
        }
        time += Time.deltaTime;
        if(time > 170)
        {
            time = 0;
            GameManager.Instance.ChangeScene("GameMenuScene");
        }
        BackGround.rectTransform.Translate(Vector3.up * Speed * Time.deltaTime);
    }
    System.Collections.IEnumerator RandomBGM()
    {
        float elapsed = 0;
        float RandomNum = Random.Range(0, 3);
        while (true)
        {
            elapsed += Time.deltaTime;
            if(elapsed > 30f)
            {
                elapsed = 0;
                RandomNum = Random.Range(0, 3);
                if(RandomNum == 0)
                {
                    AudioManager.Instance.PlayBGM(AudioManager.BGMState.Battle);
                }
                else if(RandomNum == 1)
                {
                    AudioManager.Instance.PlayBGM(AudioManager.BGMState.GameMenu);
                }
                else
                {
                    AudioManager.Instance.PlayBGM(AudioManager.BGMState.Select);
                }
            }
            yield return null;
        }
    }
}
