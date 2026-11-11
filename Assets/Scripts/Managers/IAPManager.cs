using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// In-App Purchases (Unity IAP 5, Google Play).
/// Flow: Connect -> FetchProducts -> PurchaseProduct -> OnPurchasePending -> inaam do + SAVE -> ConfirmPurchase.
/// Save ke baad hi confirm (crash ho to store dobara deta hai, inaam gum nahi). Order ID ledger: ek order ka inaam do dafa nahi.
/// Editor mein Unity ka "fake store" purchase ko foran approve karta hai (test).
/// </summary>
public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    /// <summary> Saare products ek jagah (Play Console mein yahi IDs banane hain) </summary>
    public static readonly (string id, ProductType type, int coins, string title)[] Catalog =
    {
        ("com.bradfordgames.lastthrow.coins_small",  ProductType.Consumable,     1200, "Pile of Coins"),
        ("com.bradfordgames.lastthrow.coins_medium", ProductType.Consumable,     7000, "Bag of Coins"),
        ("com.bradfordgames.lastthrow.coins_large",  ProductType.Consumable,    16000, "Chest of Coins"),
        ("com.bradfordgames.lastthrow.starter_pack", ProductType.NonConsumable,  5000, "Starter Pack"),
    };
    public const string StarterPackId = "com.bradfordgames.lastthrow.starter_pack";

    [Tooltip("Starter Pack ke saath milne wala weapon")]
    [SerializeField] private WeaponDataSO starterWeapon;
    [SerializeField] private int starterArmorLevels = 2;

    public bool IsReady { get; private set; }
    public event Action OnProductsReady;
    public event Action<string> OnPurchaseSucceeded;   // product id
    public event Action<string> OnPurchaseFailedMsg;   // message

    private StoreController store;

    async void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        try
        {
            store = UnityIAPServices.StoreController();

            // Sab events Connect se PEHLE (pichli adhoori purchase foran aa sakti hai)
            store.OnPurchasePending += OnPurchasePending;
            store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            store.OnPurchaseFailed += failed => Fail($"{failed.FailureReason}");
            store.OnPurchaseDeferred += deferred => OnPurchaseFailedMsg?.Invoke("Purchase pending approval");
            store.OnStoreConnected += OnStoreConnected;
            store.OnStoreDisconnected += failure => Debug.LogWarning($"[IAP] Store disconnected: {failure.Message}");
            store.OnProductsFetched += OnProductsFetched;
            store.OnProductsFetchFailed += failure => Debug.LogWarning($"[IAP] Product fetch failed: {failure.FailureReason}");
            store.OnPurchasesFetched += OnPurchasesFetched;
            store.OnPurchasesFetchFailed += failure => Debug.LogWarning($"[IAP] Purchases fetch failed: {failure.Message}");

            await store.Connect();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAP] Init failed: {e.Message}");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnStoreConnected()
    {
        var defs = Catalog.Select(p => new ProductDefinition(p.id, p.type)).ToList();
        store.FetchProducts(defs);
    }

    private void OnProductsFetched(List<Product> products)
    {
        IsReady = true;
        store.FetchPurchases(); // Starter pack (non-consumable) dobara install par wapis
        OnProductsReady?.Invoke();
    }

    private void OnPurchasesFetched(Orders orders)
    {
        // Pehle se khareeda Starter Pack: entitlement wapis (coins dobara nahi - sirf weapon/flag)
        foreach (var order in orders.ConfirmedOrders)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            if (product != null && product.definition.id == StarterPackId && !Economy.StarterPackOwned)
            {
                Economy.StarterPackOwned = true;
                if (starterWeapon != null) Economy.SetOwned(starterWeapon);
            }
        }
    }

    /// <summary> Localized qeemat (e.g. "Rs 280") - store tayyar na ho to "..." </summary>
    public string PriceOf(string productId)
    {
        if (!IsReady || store == null) return "...";
        var p = store.GetProductById(productId);
        return p != null && p.availableToPurchase ? p.metadata.localizedPriceString : "N/A";
    }

    public bool CanBuy(string productId)
    {
        if (!IsReady || store == null) return false;
        if (productId == StarterPackId && Economy.StarterPackOwned) return false;
        var p = store.GetProductById(productId);
        return p != null && p.availableToPurchase;
    }

    public void Buy(string productId)
    {
        if (!CanBuy(productId)) { Fail("Store not ready"); return; }
        store.PurchaseProduct(productId);
    }

    private void OnPurchasePending(PendingOrder order)
    {
        var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
        string id = product?.definition.id;
        string orderId = order.Info.TransactionID;

        // Ek hi order dobara aaye (app band ho gayi thi) to inaam dobara nahi
        if (!Economy.IsOrderProcessed(orderId))
        {
            Grant(id);
            Economy.MarkOrderProcessed(orderId); // save ho chuka (PlayerPrefs.Save)
        }
        store.ConfirmPurchase(order); // SAVE ke baad hi confirm
        OnPurchaseSucceeded?.Invoke(id);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        if (order is FailedOrder failed) Debug.LogWarning($"[IAP] Confirm failed: {failed.FailureReason} {failed.Details}");
    }

    private void Grant(string productId)
    {
        var item = Catalog.FirstOrDefault(p => p.id == productId);
        if (item.id == null) return;
        Economy.Grant(item.coins);
        if (productId == StarterPackId)
        {
            Economy.StarterPackOwned = true;
            if (starterWeapon != null) Economy.SetOwned(starterWeapon);
            Economy.AddArmorLevels(starterArmorLevels);
        }
        SoundManager.Instance?.PlayUnlock();
    }

    private void Fail(string msg)
    {
        Debug.LogWarning($"[IAP] {msg}");
        OnPurchaseFailedMsg?.Invoke(msg);
    }
}
