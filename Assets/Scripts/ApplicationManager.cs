using UnityEngine;
using System.Collections;

public class ApplicationManager : MonoBehaviour
{
    public void SetQuality(float level)
    {
        QualitySettings.SetQualityLevel((int)level, true);
    }


    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
		                Application.Quit();
#endif
    }
}
