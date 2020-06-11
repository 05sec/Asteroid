using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MagicLoadSceneOnClick : MonoBehaviour
{
    public void LoadSceneProjectiles()
    {
        SceneManager.LoadScene("magic_projectiles");
    }
    public void LoadSceneSprays()
    {
        SceneManager.LoadScene("magic_sprays");
    }
    public void LoadSceneAura()
    {
        SceneManager.LoadScene("magic_aura");
    }
    public void LoadSceneModular()
    {
        SceneManager.LoadScene("magic_modular");
    }
    public void LoadSceneShields2()
    {
        SceneManager.LoadScene("magic_domes");
    }
    public void LoadSceneShields()
    {
        SceneManager.LoadScene("magic_shields");
    }
    public void LoadSceneSphereBlast()
    {
        SceneManager.LoadScene("magic_sphereblast");
    }
    public void LoadSceneEnchant()
    {
        SceneManager.LoadScene("magic_enchant");
    }
    public void LoadSceneSlash()
    {
        SceneManager.LoadScene("magic_slash");
    }
    public void LoadSceneCharge()
    {
        SceneManager.LoadScene("magic_charge");
    }
    public void LoadSceneCleave()
    {
        SceneManager.LoadScene("magic_cleave");
    }
    public void LoadSceneAura2()
    {
        SceneManager.LoadScene("magic_aura2");
    }
    public void LoadSceneWalls()
    {
        SceneManager.LoadScene("magic_walls");
    }
	public void LoadSceneBeams()
    {
        SceneManager.LoadScene("magic_beams");
    }
	public void LoadSceneMeshGlow()
    {
        SceneManager.LoadScene("magic_meshglow");
    }
	public void LoadScenePillarBlast()
    {
        SceneManager.LoadScene("magic_pillarblast");
    }
}