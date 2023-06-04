using UnityEngine;

public class CausticGoboLightBehaviour : MonoBehaviour
{
    public Texture2D[] Frames;
    public float FramePerSecond = 15;

    private float timer = 0;
    private float interval = 1f;
    private int currentFrame = 0;
    private Light targetLight;

    public void Start()
    {
        targetLight = GetComponent<Light>();

        interval = 1.0f / FramePerSecond;
    }

    public void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = timer % interval;
            incrementFrame();
        }
    }

    private void incrementFrame()
    {
        currentFrame++;

        if (currentFrame >= Frames.Length)
        {
            currentFrame = 0;
        }

        targetLight.cookie = Frames[currentFrame];
    }
}