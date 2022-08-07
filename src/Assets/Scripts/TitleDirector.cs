using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleDirector : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.anyKey)
        {
            Invoke("ChangeScene", 1.0f);// íxâÑé¿çs
        }
    }
    void ChangeScene()
    {
        SceneManager.LoadScene("PlayScene");
    }
}