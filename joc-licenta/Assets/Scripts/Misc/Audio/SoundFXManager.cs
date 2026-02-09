using System;
using UnityEngine;

public enum SoundType
{
    ShopOpen,
    UiClick,
    ShopClose_Register, // Sunetul mecanic (mereu același)
    ShopClose_Jingle
}

[System.Serializable]
public class SoundItem
{
    [HideInInspector] public string name; // Doar ca să apară numele în listă automat
    public SoundType type;       // Aici selectezi tu din meniu (Open/Close)
    public AudioClip clip;       // Aici tragi fișierul audio
    [Range(0f, 1f)] public float volume = 1f; // Volum individual per sunet
}

public class SoundFXManager : MonoBehaviour
{
    public static SoundFXManager Instance;
    [Header("Referințe")]
    [SerializeField] private AudioSource soundFXObject; // Prefab-ul AudioSource
    [SerializeField] private AudioDataBase audioDatabase;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public void PlaySound(SoundType type, Transform spawnTransform)
    {
        if (audioDatabase == null)
        {
            Debug.LogWarning("[SoundFXManager] Nu ai asignat AudioDatabaseSO în Inspector!");
            return;
        }

        // 1. Cerem SO-ului clipul și volumul asociat
        AudioClip clip = audioDatabase.GetClip(type, out float volumeFromSO);

        // 2. Dacă există, îl redăm folosind metoda ta de bază
        if (clip != null)
        {
            PlaySoundFXClip(clip, spawnTransform, volumeFromSO);
        }
    }

    public void PlaySoundFXClip(AudioClip clip, Transform spawnTransform, float volume = 1.0f)
    {
        if (clip == null) return;

        // Verificare încărcare (bună practică)
        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            clip.LoadAudioData();
        }

        // Safety check: dacă spawnTransform e null, redăm la poziția managerului
        Vector3 spawnPos = spawnTransform != null ? spawnTransform.position : transform.position;

        // Instanțiem sursa
        AudioSource audioSource = Instantiate(soundFXObject, spawnPos, Quaternion.identity);

        audioSource.clip = clip;
        audioSource.volume = volume; // Aici se aplică volumul setat în SO

        // Asigură-te că spatialBlend e setat corect pe prefab (0 = 2D, 1 = 3D)
        // audioSource.spatialBlend = 1f; 

        audioSource.Play();

        float clipLength = audioSource.clip.length;

        // Distrugem obiectul după ce termină de cântat
        Destroy(audioSource.gameObject, clipLength);
    }
}
