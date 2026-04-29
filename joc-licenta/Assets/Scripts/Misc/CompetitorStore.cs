using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CompetitorStore", menuName = "Scriptable Objects/CompetitorStore")]
public class CompetitorStore : ScriptableObject
{
    [Header("Identitate")]
    public string storeName = "Magazin Concurent";

    [Tooltip("Descriere scurtă pentru UI.")]
    [TextArea(2, 3)] public string description;

    public Sprite storeIcon;

    [Header("Produse Vandute")]
    [Tooltip("Lista exacta de produse pe care le vinde acest competitor.")]
    public List<ProductType> soldProducts = new List<ProductType>();

    [Header("Specialitate")]
    [Tooltip("Tipurile de produse la care acest concurent e mai competitiv.")]
    public List<ProductType> specialtyProducts = new List<ProductType>();

    [Tooltip("Multiplicator preț pentru produsele de specialitate (ex: 0.85 = 15% mai ieftin).")]
    [Range(0.5f, 1.5f)] public float specialtyPriceMultiplier = 0.85f;

    [Tooltip("Multiplicator preț pentru produsele non-specialitate.")]
    [Range(0.5f, 2f)] public float defaultPriceMultiplier = 1.1f;

    [Header("Comportament")]
    [Tooltip("Cât de repede reacționează la prețurile jucătorului (0 = deloc, 1 = instant).")]
    [Range(0f, 1f)] public float reactivityToPlayer = 0.3f;

    [Tooltip("Variație random zilnică aplicată prețurilor (ex: 0.05 = ±5%).")]
    [Range(0f, 0.2f)] public float dailyPriceVariation = 0.05f;
}