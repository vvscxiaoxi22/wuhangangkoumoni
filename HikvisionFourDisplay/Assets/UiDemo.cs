using System;
using UnityEngine;
using UnityEngine.UI;
public class UiDemo : MonoBehaviour
{
    public Text 时间;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        时间.text = DateTime.Now.ToString();
    }
}
