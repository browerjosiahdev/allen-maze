using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class FinishWallController : MonoBehaviour {

	void OnTriggerEnter( Collider other )
    {
        Text finishedText = GameObject.Find("Finished Text").GetComponent<Text>();

        if (finishedText != null)
        {
            finishedText.text = "YOU DID IT!";
        }
    }
}
