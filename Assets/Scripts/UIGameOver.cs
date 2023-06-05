using UnityEngine;

public class UIGameOver : MonoBehaviour
{
    private Canvas _gameOverCanvas;
    // Start is called before the first frame update
    void Start()
    {
        _gameOverCanvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        PlayerController.GameOverEvent += GameOver;
    }

    private void OnDisable()
    {
        PlayerController.GameOverEvent -= GameOver;
    }

    private void GameOver()
    {
        _gameOverCanvas.enabled = true;
    }
}
