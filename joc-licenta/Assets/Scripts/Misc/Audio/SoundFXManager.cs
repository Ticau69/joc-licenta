using System;
using UnityEngine;

public enum SoundType
{
    ShopOpen,
    UiClick,
    ShopClose_Register,
    ShopClose_Jingle,
    PlaceFurniture,
    PlaceStructure,
}

[System.Serializable]
public class SoundItem
{
    [HideInInspector] public string name;
    public SoundType type;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}

public class SoundFXManager : MonoBehaviour
{
    public static SoundFXManager Instance;

    [Header("Referințe")]
    [SerializeField] private AudioSource soundFXObject;
    [SerializeField] private AudioDataBase audioDatabase;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    // Am adăugat '= null' pentru a nu da eroare la sunetele generale (fără locație)
    public void PlaySound(SoundType type, Transform spawnTransform = null)
    {
        if (audioDatabase == null)
        {
            Debug.LogWarning("[SoundFXManager] Nu ai asignat AudioDatabaseSO în Inspector!");
            return;
        }

        AudioClip clip = audioDatabase.GetClip(type, out float volumeFromSO);

        if (clip != null)
        {
            PlaySoundFXClip(clip, spawnTransform, volumeFromSO);
        }
    }

    public void PlaySoundFXClip(AudioClip clip, Transform spawnTransform, float volume = 1.0f)
    {
        if (clip == null) return;

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            clip.LoadAudioData();
        }

        // NOU: Nu mai depindem de Cameră! Folosim poziția acestui Manager ca bază.
        Vector3 spawnPos = transform.position;

        if (spawnTransform != null)
        {
            spawnPos = spawnTransform.position; // Folosim locația mobilei, dacă există
        }

        AudioSource audioSource = Instantiate(soundFXObject, spawnPos, Quaternion.identity);

        audioSource.clip = clip;
        audioSource.volume = volume;

        // SOLUȚIA SUPREMĂ: Forțăm sunetul să fie 2D direct din cod!
        // Ignoră complet distanța și se va auzi la fel de tare oriunde.
        audioSource.spatialBlend = 0f;

        audioSource.Play();
        Destroy(audioSource.gameObject, clip.length);
    }
}