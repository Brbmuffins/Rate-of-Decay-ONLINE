using System.Collections;
using UnityEngine;

public class RandomBlink : MonoBehaviour
{
    public Vector3 openRotation = new Vector3(-3.13f, 89.623f, -24.03f);
    public Vector3 closedRotation = new Vector3(-3.13f, 89.623f, 87f);

    public float closeSpeed = 0.04f;
    public float holdClosedTime = 0.05f;
    public float openSpeed = 0.06f;

    public float minBlinkDelay = 2f;
    public float maxBlinkDelay = 6f;

    private bool blinking = false;

    void Start()
    {
        transform.localEulerAngles = openRotation;
        StartCoroutine(BlinkLoop());
    }

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minBlinkDelay, maxBlinkDelay));

            if (!blinking)
                yield return StartCoroutine(Blink());
        }
    }

    IEnumerator Blink()
    {
        blinking = true;

        yield return RotateTo(closedRotation, closeSpeed);
        yield return new WaitForSeconds(holdClosedTime);
        yield return RotateTo(openRotation, openSpeed);

        blinking = false;
    }

    IEnumerator RotateTo(Vector3 targetRotation, float duration)
    {
        Quaternion start = transform.localRotation;
        Quaternion end = Quaternion.Euler(targetRotation);

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            transform.localRotation = Quaternion.Lerp(start, end, timer / duration);
            yield return null;
        }

        transform.localRotation = end;
    }
}