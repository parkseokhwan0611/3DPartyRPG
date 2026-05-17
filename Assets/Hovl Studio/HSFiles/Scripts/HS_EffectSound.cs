using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HS_EffectSound : MonoBehaviour
{
    public bool Repeating = true;
    public float RepeatTime = 2.0f;
    public float StartTime = 0.0f;
    public bool RandomVolume;
    public float minVolume = .4f;
    public float maxVolume = 1f;
    private AudioClip clip;

    private AudioSource soundComponent;

    void Awake()
    {
        soundComponent = GetComponent<AudioSource>();
        clip = soundComponent.clip;
    }

    void OnEnable()
    {
        if (RandomVolume)
            soundComponent.volume = Random.Range(minVolume, maxVolume);

        if (Repeating)
            InvokeRepeating(nameof(RepeatSound), StartTime, RepeatTime);
        else
            RepeatSound();
    }

    void OnDisable()
    {
        CancelInvoke(nameof(RepeatSound));
        soundComponent.Stop();
    }

    void RepeatSound()
    {
        if (!gameObject.activeInHierarchy) return;
        soundComponent.PlayOneShot(clip);
    }
}
