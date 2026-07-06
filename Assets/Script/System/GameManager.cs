using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("#GameControl")]
    public static GameManager instance;
    public bool isLive;

    void Awake() {
        if(GameManager.instance == null) {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else {
            Destroy (gameObject);
        }
    }
}
