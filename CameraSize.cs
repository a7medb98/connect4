using UnityEngine;

namespace ConnectFour
{
	public class CameraSize : MonoBehaviour 
	{
		Camera cam;
		
		void Awake () 
		{
			cam = GetComponent<Camera>();
			cam.orthographic = true;
		}
		
		//Makes camera larger, thus making the board look smaller
		void LateUpdate()
		{
			float maxY = (GameObject.Find ("GameController").GetComponent<GameController>().numRows) + 3;

			cam.orthographicSize = maxY / 2f;
		}
	}
}
