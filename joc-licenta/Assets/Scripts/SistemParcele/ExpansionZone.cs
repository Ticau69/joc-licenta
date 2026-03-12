using UnityEngine;
using UnityEngine.InputSystem; // NOU: Folosim noul sistem
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class ExpansionZone : MonoBehaviour
{
    [Header("Setări Parcelă")]
    public int unlockCost = 15000;

    [Header("Podeaua care se deblochează")]
    [Tooltip("Trage aici obiectul Plane care trebuie să primească layer-ul Placement")]
    public GameObject floorPlane;

    [Header("Vizualizare Grid Parcelă")]
    [Tooltip("Trage aici obiectul gridVisualization (pătrățelele albe) de pe ACEASTĂ parcelă")]
    public GameObject myGridVisual;

    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        // 1. Verificăm dacă jucătorul a apăsat click stânga fix în acest frame
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 2. Ignorăm dacă a dat click pe un buton din UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // 3. Tragem o "rază laser" din cameră spre poziția mouse-ului
            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;

            // 4. Verificăm dacă raza a lovit ceva
            if (Physics.Raycast(ray, out hit, 100f))
            {
                // Dacă obiectul lovit de rază este CHIAR ACEST obiect, înseamnă că am dat click pe el!
                if (hit.collider.gameObject == this.gameObject)
                {
                    TryBuyZone();
                }
            }
        }
    }

    public void TryBuyZone()
    {
        if (ServiceLocator.Instance.TryGet(out IMoneyService money))
        {
            if (money.CanAfford(unlockCost))
            {
                money.TrySpend(unlockCost);

                if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
                {
                    eventBus.Publish(new ShowNotificationEvent(
                        "Extindere Finalizată",
                        "Ai deblocat o nouă parcelă pentru magazinul tău!",
                        NotificationType.Success));
                }

                UnlockLand();
            }
            else
            {
                if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
                {
                    eventBus.Publish(new ShowNotificationEvent(
                        "Fonduri Insuficiente",
                        $"Ai nevoie de {unlockCost} RON pentru a cumpăra această parcelă.",
                        NotificationType.Error));
                }
            }
        }
    }

    private void UnlockLand()
    {
        // 1. Schimbăm layer-ul podelei în "Placement"
        int placementLayer = LayerMask.NameToLayer("Placement");

        if (floorPlane != null)
        {
            floorPlane.layer = placementLayer;
            foreach (Transform child in floorPlane.transform)
            {
                child.gameObject.layer = placementLayer;
            }
        }

        // --- NOU: Adăugăm grid-ul nou în Placement System ---
        PlacementSystem placementSystem = UnityEngine.Object.FindFirstObjectByType<PlacementSystem>();
        if (placementSystem != null && myGridVisual != null)
        {
            placementSystem.AddGridVisual(myGridVisual);
        }
        else
        {
            Debug.LogWarning("[ExpansionZone] Nu s-a găsit PlacementSystem sau myGridVisual e gol!");
        }
        // ----------------------------------------------------

        // 2. OPRIM Collider-ul în loc să îl distrugem
        if (GetComponent<Collider>() != null)
        {
            GetComponent<Collider>().enabled = false;
        }

        CameraController camController = UnityEngine.Object.FindFirstObjectByType<CameraController>();
        if (camController != null && myGridVisual != null)
        {
            // Luăm componenta vizuală DIRECT de pe variabila ta!
            Renderer gridRenderer = myGridVisual.GetComponent<Renderer>();

            // Plasa de siguranță: dacă e pusă pe un copil al lui myGridVisual
            if (gridRenderer == null)
            {
                gridRenderer = myGridVisual.GetComponentInChildren<Renderer>();
            }

            if (gridRenderer != null)
            {
                camController.AddMapRenderer(gridRenderer);
            }
            else
            {
                Debug.LogWarning("[ExpansionZone] Nu s-a găsit niciun Renderer pe myGridVisual!");
            }
        }

        // 3. Ne distrugem pe noi
        Destroy(this);
    }
}