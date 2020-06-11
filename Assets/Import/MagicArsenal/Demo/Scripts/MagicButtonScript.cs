using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace MagicArsenal
{

public class MagicButtonScript : MonoBehaviour
{
	public GameObject Button;
	Text MyButtonText;
	string projectileParticleName;		// The variable to update the text component of the button

	MagicFireProjectile effectScript;		// A variable used to access the list of projectiles
	MagicProjectileScript projectileScript;

	public float buttonsX;
	public float buttonsY;
	public float buttonsSizeX;
	public float buttonsSizeY;
	public float buttonsDistance;
	
	void Start ()
	{
		effectScript = GameObject.Find("MagicFireProjectile").GetComponent<MagicFireProjectile>();
		getProjectileNames();
		MyButtonText = Button.transform.Find("Text").GetComponent<Text>();
		MyButtonText.text = projectileParticleName;
	}

	void Update ()
	{
		MyButtonText.text = projectileParticleName;
//		print(projectileParticleName);
	}

	public void getProjectileNames()			// Find and diplay the name of the currently selected projectile
	{
		// Access the currently selected projectile's 'ProjectileScript'
		projectileScript = effectScript.projectiles[effectScript.currentProjectile].GetComponent<MagicProjectileScript>();
		projectileParticleName = projectileScript.projectileParticle.name;	// Assign the name of the currently selected projectile to projectileParticleName
	}

	public bool overButton()		// This function will return either true or false
	{
		Rect button1 = new Rect(buttonsX, buttonsY, buttonsSizeX, buttonsSizeY);
		Rect button2 = new Rect(buttonsX + buttonsDistance, buttonsY, buttonsSizeX, buttonsSizeY);
		
		if(button1.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)) ||
		   button2.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
		{
			return true;
		}
		else
			return false;
	}
}
}