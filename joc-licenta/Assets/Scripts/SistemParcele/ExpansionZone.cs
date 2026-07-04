using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider))]
public class ExpansionZone : MonoBehaviour
{
    [Header("Setări Parcelă")]
    public string plotID;
    public int unlockCost = 15000;
    public GameObject floorPlane;
    public GameObject myGridVisual;

    [Header("Vizualizare & UI")]
    public MeshRenderer cubeVisual;
    public UIDocument costUIDocument;

    [Header("Culori Stare")]
    public Color availableColor = new Color(0.2f, 0.8f, 0.2f, 0.4f); // Verde transparent
    public Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

    private Collider _collider;
    private bool _isExpandModeActive = false;
    private bool _isAvailableToBuy = false;
    private Button _buyButton;

    private void Start()
    {
        _collider = GetComponent<Collider>();

        // 1. Oprim vizualurile la start
        if (cubeVisual != null) cubeVisual.enabled = false;
        if (_collider != null) _collider.enabled = false;

        // 2. Setup UI Toolkit (Găsim textul și butonul verde!)
        if (costUIDocument != null && costUIDocument.rootVisualElement != null)
        {
            Label priceLabel = costUIDocument.rootVisualElement.Q<Label>("PlotCost");
            if (priceLabel != null) priceLabel.text = $"{unlockCost:N0} RON";

            _buyButton = costUIDocument.rootVisualElement.Q<Button>("BuyButton");
            if (_buyButton != null)
            {
                // AICI LEGĂM BUTONUL! Când e apăsat, apelează TryBuyZone
                _buyButton.clicked += TryBuyZone;
            }

            costUIDocument.rootVisualElement.style.display = DisplayStyle.None;
        }

        // 3. Ne abonăm la radio
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Subscribe<ToggleExpansionModeEvent>(OnToggleExpansionMode);
            eventBus.Subscribe<CloseAllUIEvent>(OnCloseAllUI);
            eventBus.Subscribe<PlotUnlockedEvent>(OnNeighborUnlocked); // Ascultăm dacă un vecin a fost cumpărat
        }

        if (LandExpansionManager.Instance != null)
            LandExpansionManager.Instance.RegisterZone(this);
    }

    private void OnDestroy()
    {
        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Unsubscribe<ToggleExpansionModeEvent>(OnToggleExpansionMode);
            eventBus.Unsubscribe<CloseAllUIEvent>(OnCloseAllUI);
            eventBus.Unsubscribe<PlotUnlockedEvent>(OnNeighborUnlocked); // Linia lipsă
        }

        if (_buyButton != null)
            _buyButton.clicked -= TryBuyZone;
    }

    private void OnToggleExpansionMode(ToggleExpansionModeEvent e)
    {
        _isExpandModeActive = e.IsActive;

        if (_isExpandModeActive)
        {
            // Începem procesul de evaluare în fundal (fără să arătăm cubul încă!)
            _ = EvaluateAndShowRoutine();
        }
        else
        {
            // Oprim orice calcul în așteptare
            StopAllCoroutines();

            // Stingem totul instantaneu la ieșirea din mod
            if (cubeVisual != null) cubeVisual.enabled = false;
            if (costUIDocument != null && costUIDocument.rootVisualElement != null)
            {
                costUIDocument.rootVisualElement.style.display = DisplayStyle.None;
            }
        }
    }

    private async Awaitable EvaluateAndShowRoutine()
    {
        // Așteptăm 2 cadre de fizică (FixedUpdate) pentru a fi 100% siguri 
        // că PlacementSystem a aprins gridul și Unity l-a înregistrat
        await Awaitable.FixedUpdateAsync();
        await Awaitable.FixedUpdateAsync();

        // 1. Facem calculele de proximitate (Asta va seta _isAvailableToBuy și va alege culoarea corectă)
        EvaluateAvailability();

        // 2. ABIA ACUM aprindem cubul! (Va apărea direct verde sau gri, fără acel "flash" ciudat)
        if (cubeVisual != null) cubeVisual.enabled = true;

        // 3. Aprindem și UI-ul DOAR dacă parcela e verde (disponibilă)
        if (costUIDocument != null && costUIDocument.rootVisualElement != null)
        {
            costUIDocument.rootVisualElement.style.display = _isAvailableToBuy ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void EvaluateAvailability()
    {
        Vector3 center = transform.position;
        // Ajustăm distanța de scanare: jumătate din mărimea parcelei tale + o mică marjă
        // Dacă parcela are 10x10, scanăm la 5.5 unități distanță.
        float scanDistance = 5.5f;
        int placementLayerMask = 1 << LayerMask.NameToLayer("Placement");

        // Verificăm în cele 4 direcții cardinale (NU pe diagonală)
        bool hitRight = Physics.CheckSphere(center + Vector3.right * scanDistance, 0.5f, placementLayerMask);
        bool hitLeft = Physics.CheckSphere(center + Vector3.left * scanDistance, 0.5f, placementLayerMask);
        bool hitForward = Physics.CheckSphere(center + Vector3.forward * scanDistance, 0.5f, placementLayerMask);
        bool hitBack = Physics.CheckSphere(center + Vector3.back * scanDistance, 0.5f, placementLayerMask);

        // Parcela este disponibilă DOAR dacă se atinge de o latură, nu de un colț
        _isAvailableToBuy = hitRight || hitLeft || hitForward || hitBack;

        if (cubeVisual != null)
        {
            cubeVisual.material.color = _isAvailableToBuy ? availableColor : lockedColor;
        }
    }

    private void OnNeighborUnlocked(PlotUnlockedEvent e)
    {
        if (_isExpandModeActive)
        {
            EvaluateAvailability();

            // Re-evaluăm și UI-ul dacă un vecin s-a deblocat!
            if (costUIDocument != null && costUIDocument.rootVisualElement != null)
            {
                costUIDocument.rootVisualElement.style.display = _isAvailableToBuy ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }

    private void OnCloseAllUI(CloseAllUIEvent e)
    {
        // Forțăm ascunderea dacă s-a deschis alt panou (ex: Angajați)
        OnToggleExpansionMode(new ToggleExpansionModeEvent(false));
    }

    public void TryBuyZone()
    {
        if (!_isAvailableToBuy) return; // Plasa de siguranță: nu poți cumpăra dacă e gri!

        if (ServiceLocator.Instance.TryGet(out IMoneyService money))
        {
            if (money.CanAfford(unlockCost))
            {
                money.TrySpend(unlockCost);
                UnlockLand();
            }
        }
    }

    private void UnlockLand()
    {
        int placementLayer = LayerMask.NameToLayer("Placement");

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        gameObject.layer = placementLayer;

        if (cubeVisual != null) cubeVisual.enabled = false;

        Transform gridVis = transform.Find("gridVisualization");
        if (gridVis != null)
        {
            gridVis.gameObject.layer = placementLayer;

            MeshRenderer meshRenderer = gridVis.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
                meshRenderer.material.SetColor("_Color", Color.white);
            }
        }

        if (LandExpansionManager.Instance != null)
        {
            LandExpansionManager.Instance.MarkAsUnlocked(plotID);
            LandExpansionManager.Instance.UnregisterZone(this);
        }

        // FIX: Dezabonare înainte de Publish
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Unsubscribe<ToggleExpansionModeEvent>(OnToggleExpansionMode);
            eventBus.Unsubscribe<CloseAllUIEvent>(OnCloseAllUI);
            eventBus.Unsubscribe<PlotUnlockedEvent>(OnNeighborUnlocked);
        }

        // Extindem limitele camerei cu podeaua acestei parcele
        CameraController cam = UnityEngine.Object.FindFirstObjectByType<CameraController>();
        if (cam != null)
        {
            // Folosim floorPlane dacă e setat, altfel căutăm un Renderer direct pe obiect
            if (floorPlane != null)
            {
                Renderer floorRenderer = floorPlane.GetComponentInChildren<Renderer>();
                if (floorRenderer != null)
                    cam.AddMapRenderer(floorRenderer);
                else
                    Debug.LogWarning("[UNLOCK] floorPlane nu are Renderer nici pe copii!");
            }
            else
            {
                // Fallback: folosim gridVisualization ca referință de bounds
                Transform gv = transform.Find("gridVisualization");
                if (gv != null)
                {
                    Renderer gvRenderer = gv.GetComponent<Renderer>();
                    if (gvRenderer != null)
                        cam.AddMapRenderer(gvRenderer);
                }
                Debug.LogWarning("[UNLOCK] floorPlane nu e setat în Inspector! Folosesc gridVisualization ca fallback.");
            }
        }
        else
        {
            Debug.LogError("[UNLOCK] CameraController nu a fost găsit în scenă!");
        }

        PlacementSystem placementSystem = UnityEngine.Object.FindFirstObjectByType<PlacementSystem>();
        if (placementSystem != null)
            placementSystem.AddGridVisual(this.gameObject);

        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus2))
            eventBus2.Publish(new PlotUnlockedEvent());

        if (costUIDocument != null)
            costUIDocument.rootVisualElement.style.display = DisplayStyle.None;

        if (_buyButton != null) _buyButton.clicked -= TryBuyZone;

        Destroy(this);
    }

    public void LoadUnlock()
    {
        int placementLayer = LayerMask.NameToLayer("Placement");

        // --- NOU: REPORNIM FIZICA ȘI LAYER-UL ---
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true; // Permitem mouse-ului să lovească podeaua
        gameObject.layer = placementLayer;   // Punem parcela pe layer-ul de construire

        if (cubeVisual != null) cubeVisual.enabled = false;

        Transform gridVis = transform.Find("gridVisualization");
        if (gridVis != null)
        {
            gridVis.gameObject.layer = placementLayer;

            MeshRenderer meshRenderer = gridVis.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
                meshRenderer.material.SetColor("_Color", Color.white);
            }
        }

        CameraController cam = UnityEngine.Object.FindFirstObjectByType<CameraController>();
        if (cam != null)
        {
            if (floorPlane != null)
            {
                Renderer floorRenderer = floorPlane.GetComponentInChildren<Renderer>();
                if (floorRenderer != null) cam.AddMapRenderer(floorRenderer);
            }
            else
            {
                Transform gv = transform.Find("gridVisualization");
                if (gv != null)
                {
                    Renderer gvRenderer = gv.GetComponent<Renderer>();
                    if (gvRenderer != null) cam.AddMapRenderer(gvRenderer);
                }
            }
        }

        PlacementSystem placementSystem = UnityEngine.Object.FindFirstObjectByType<PlacementSystem>();
        if (placementSystem != null)
            placementSystem.AddGridVisual(this.gameObject);

        if (costUIDocument != null)
            costUIDocument.rootVisualElement.style.display = DisplayStyle.None;

        if (LandExpansionManager.Instance != null)
            LandExpansionManager.Instance.UnregisterZone(this);

        Destroy(this);
    }
}