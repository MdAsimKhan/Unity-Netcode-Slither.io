using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ClientEatSound : Singleton<ClientEatSound>
{
    private AudioSource audioSource;
    
    [SerializeField] private AudioClip eatSound;

    public override void Awake()
    {
        base.Awake();
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayEatSound()
    {
        audioSource.clip = eatSound;
        audioSource.Play();
    }
}
