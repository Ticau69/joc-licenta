using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "AudioDataBase", menuName = "Scriptable Objects/AudioDataBase")]
public class AudioDataBase : ScriptableObject
{
    [System.Serializable]
    public class SoundItem
    {
        [HideInInspector] public string name;
        public SoundType type;

        [Header("Sunete Comune (Standard)")]
        public AudioClip[] commonClips;

        [Header("Sunete Rare (Special)")]
        public AudioClip[] rareClips;
        [Range(0f, 1f)] public float rareChance = 0.1f; // 10% șansă default

        [Range(0f, 1f)] public float volume = 1f;
    }

    public List<SoundItem> sounds;

    public AudioClip GetClip(SoundType type, out float volume)
    {
        SoundItem item = sounds.FirstOrDefault(s => s.type == type);

        if (item != null)
        {
            volume = item.volume;
            AudioClip[] selectedPool = item.commonClips;

            // --- LOGICA DE RARITATE ---
            // Dacă avem clipuri rare ȘI zarul pică sub șansă (ex: 0.1)
            if (item.rareClips != null && item.rareClips.Length > 0)
            {
                if (Random.value <= item.rareChance)
                {
                    selectedPool = item.rareClips;
                    // Debug.Log("★ JACKPOT! Sunet rar redat!");
                }
            }

            // Alegem random din pool-ul selectat (Comun sau Rar)
            if (selectedPool != null && selectedPool.Length > 0)
            {
                int randomIndex = Random.Range(0, selectedPool.Length);
                return selectedPool[randomIndex];
            }
        }

        volume = 1f;
        return null;
    }

    private void OnValidate()
    {
        foreach (var s in sounds)
        {
            s.name = s.type.ToString();
        }
    }
}
