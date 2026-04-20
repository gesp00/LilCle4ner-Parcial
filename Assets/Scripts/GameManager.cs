using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameObject defeatCanvas;


    [SerializeField] private GameObject victoryCanvas;

    private bool gameOver = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        
        if (defeatCanvas != null) defeatCanvas.SetActive(false);
        if (victoryCanvas != null) victoryCanvas.SetActive(false);
    }

   

 
    public void TriggerDefeat()
    {
        if (gameOver) return;
        gameOver = true;

        Time.timeScale = 0f;

        if (defeatCanvas != null)
            defeatCanvas.SetActive(true);
        else
            Debug.LogWarning("defeatCanvas no asignado en el Inspector.");
    }

   
    public void TriggerVictory()
    {
        if (gameOver) return;
        gameOver = true;

        Time.timeScale = 0f;

        if (victoryCanvas != null)
            victoryCanvas.SetActive(true);
        else
            Debug.LogWarning("[GameManager] victoryCanvas no asignado. Podés crearlo después.");
    }

  
    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

  
    public void Quit()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }
}
