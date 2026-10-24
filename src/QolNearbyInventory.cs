using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sirroh.Qol
{
    /// <summary>
    /// Key-activated only: no per-frame chest scans, per-frame network requests or
    /// item-name cross-products. Ownership acquisition is awaited before mutation.
    /// </summary>
    internal static class QolNearbyInventory
    {
        private const float RangeSq = 100f * 100f;
        private static Plugin _host;
        private const float CellSize = 32f;
        private const double FrameBudgetMs = 2.0; // strict co-operative time slice
        private static readonly Dictionary<long,List<Container>> Cells = new Dictionary<long,List<Container>>();
        private static readonly Dictionary<Container,long> Locations = new Dictionary<Container,long>();
        private static readonly List<Container> Nearby = new List<Container>(160);
        private static readonly HashSet<string> Wanted = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string,List<ItemDrop.ItemData>> Contents =
            new Dictionary<string,List<ItemDrop.ItemData>>(StringComparer.Ordinal);
        private static readonly Stack<List<ItemDrop.ItemData>> ListPool =
            new Stack<List<ItemDrop.ItemData>>(32);
        private static readonly Stopwatch Budget = new Stopwatch();
        private static readonly List<ItemDrop.ItemData> PlayerItems = new List<ItemDrop.ItemData>(64);
        private static readonly FieldInfo ContainerOpenEffects = AccessTools.Field(typeof(Container), "m_openEffects");
        private static bool _busy;
        private static int _lastStartFrame = -1;
        private static GameObject _pingPrefab;
        private static readonly Type LocalizationType = AccessTools.TypeByName("Localization");
        private static readonly FieldInfo LocalizationSingleton = LocalizationType == null ? null :
            AccessTools.Field(LocalizationType, "instance");
        private static readonly MethodInfo LocalizationMethod = FindLocalizationMethod();
        private static float _nextKey;

        internal static void Install(Plugin host)
        {
            _host = host;
            host.Patch("QOL.Containers.Awake", AccessTools.Method(typeof(Container), "Awake"),
                postfix: AccessTools.Method(typeof(QolNearbyInventory), nameof(OnContainerAwake)));

        }

        private static long Key(int x, int z) => ((long)x << 32) | (uint)z;

        private static long CellOf(Vector3 position) =>
            Key(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.z / CellSize));

        private static void OnContainerAwake(Container __instance)
        {
            if (__instance == null || Locations.ContainsKey(__instance)) return;
            long sector = CellOf(__instance.transform.position);
            Locations.Add(__instance,sector);
            if (!Cells.TryGetValue(sector,out List<Container> list))
                Cells[sector] = list = new List<Container>(8);
            list.Add(__instance);
            // Container itself has no OnDestroy in Valheim 1.0.
            // A tiny tracker removes the bucket entry when its GO unloads.
            QolChestTracker tracker = __instance.GetComponent<QolChestTracker>();
            if (tracker == null) tracker = __instance.gameObject.AddComponent<QolChestTracker>();
            tracker.Bind(__instance);
        }

        internal static void OnContainerDestroy(Container __instance)
        {
            if (__instance == null || !Locations.TryGetValue(__instance,out long key)) return;
            Locations.Remove(__instance);
            if (Cells.TryGetValue(key,out List<Container> list))
            {
                list.Remove(__instance);
                if (list.Count == 0) Cells.Remove(key);
            }
        }

        private static void ClearContentIndex()
        {
            foreach (List<ItemDrop.ItemData> list in Contents.Values)
            {
                list.Clear();
                if (ListPool.Count < 128) ListPool.Push(list);
            }
            Contents.Clear();
        }

        private static void IndexInventory(Inventory inv)
        {
            ClearContentIndex();
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                if (item == null || item.m_shared == null || item.m_stack < 1) continue;
                string name = item.m_shared.m_name;
                if (!Wanted.Contains(name)) continue;
                if (!Contents.TryGetValue(name,out List<ItemDrop.ItemData> list))
                {
                    list = ListPool.Count > 0 ? ListPool.Pop() : new List<ItemDrop.ItemData>(4);
                    Contents[name] = list;
                }
                list.Add(item);
            }
        }

        private static bool Relevant(Inventory inv, bool quick)
        {
            IndexInventory(inv);
            if (Contents.Count == 0) return false;
            if (quick) return true;
            foreach (ItemDrop.ItemData item in PlayerItems)
                if (item != null && item.m_shared != null &&
                    item.m_stack < item.m_shared.m_maxStackSize &&
                    Contents.ContainsKey(item.m_shared.m_name))
                    return true;
            return false;
        }

        internal static void Tick()
        {
            if (_host == null || _busy || Time.unscaledTime < _nextKey || !Application.isFocused ||
                Console.IsVisible()) return;
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.IsTeleporting() || InventoryGui.instance == null)
                return;
            if (QolGameAccess.DragItem(InventoryGui.instance) != null) return;
            GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.GetComponentInParent<TMP_InputField>() != null) return;
            bool quick = Input.GetKeyDown(KeyCode.Q);
            bool restock = !quick && Input.GetKeyDown(KeyCode.R);
            if (!quick && !restock) return;
            _nextKey = Time.unscaledTime + .25f;
            if (quick && QolAzuAutoStoreCompatibility.DispatchQuickStack()) return;
            _host.StartCoroutine(Execute(player, quick));
        }

        internal static void StartArea(bool quick)
        {
            if (_host == null || _busy || Player.m_localPlayer == null) return;
            if (quick && _lastStartFrame == Time.frameCount) return;
            if (quick) _lastStartFrame = Time.frameCount;
            if (quick && QolAzuAutoStoreCompatibility.DispatchQuickStack()) return;
            _host.StartCoroutine(Execute(Player.m_localPlayer, quick));
        }

        internal static void StartStoreCurrent()
        {
            if (_host == null || _busy || Player.m_localPlayer == null) return;
            _host.StartCoroutine(StoreCurrent(Player.m_localPlayer));
        }

        private static IEnumerator StoreCurrent(Player player)
        {
            _busy = true;
            try
            {
                InventoryGui gui = InventoryGui.instance;
                Container chest = gui == null ? null : QolGameAccess.CurrentContainer(gui);
                if (chest == null || !CanAccess(chest, player))
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Otwórz dostępną skrzynię.");
                    yield break;
                }
                ZNetView view = QolGameAccess.View(chest);
                if (!view.IsOwner())
                {
                    view.ClaimOwnership();
                    float deadline = Time.realtimeSinceStartup + .9f;
                    while (view != null && !view.IsOwner() && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }
                if (view == null || !view.IsOwner() || !CanAccess(chest, player))
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Brak uprawnień do zapisu w skrzyni.");
                    yield break;
                }
                Inventory playerInv = player.GetInventory();
                Inventory chestInv = QolGameAccess.Inventory(chest);
                if (playerInv == null || chestInv == null) yield break;
                var snapshot = new List<ItemDrop.ItemData>(playerInv.GetAllItems());
                int moved = 0;
                foreach (ItemDrop.ItemData item in snapshot)
                {
                    if (item == null || !playerInv.ContainsItem(item) ||
                        item.m_gridPos.y == 0 || item.m_equipped ||
                        QolFavorites.IsFavorite(item, playerInv)) continue;
                    int before = item.m_stack;
                    chestInv.MoveItemToThis(playerInv, item);
                    moved += playerInv.ContainsItem(item) ? Math.Max(0, before - item.m_stack) : before;
                }
                if (moved > 0)
                {
                    QolGameAccess.Save(chest);
                    PlayFeedback(chest, player);
                }
                player.Message(MessageHud.MessageType.TopLeft, "Store All: przeniesiono " + moved + " szt.");
            }
            finally
            {
                _busy = false;
            }
        }

        private static IEnumerator Execute(Player player, bool quick)
        {
            if (_busy) yield break;
            _busy = true;
            int moved = 0, touched = 0, skipped = 0;
            Container firstModified = null;
            string firstItem = null;
            bool multipleTypes = false;
            Budget.Restart();
            try
            {
                Inventory src = player == null ? null : player.GetInventory();
                if (src == null) yield break;
                GetContainers(player);
                if (Nearby.Count == 0)
                {
                    player.Message(MessageHud.MessageType.TopLeft,
                        "Brak wczytanych skrzyń w zasięgu 100 m.");
                    yield break;
                }

                PlayerItems.Clear();
                Wanted.Clear();
                foreach (ItemDrop.ItemData item in src.GetAllItems())
                {
                    if (item == null || item.m_shared == null) continue;
                    if (quick && item.m_gridPos.y == 0) continue;
                    if (item.m_equipped || item.m_shared.m_maxStackSize <= 1) continue;
                    if (quick && QolFavorites.IsFavorite(item,src)) continue;
                    if (!quick && item.m_stack >= item.m_shared.m_maxStackSize) continue;
                    PlayerItems.Add(item);
                    Wanted.Add(item.m_shared.m_name);
                }
                if (PlayerItems.Count == 0)
                {
                    player.Message(MessageHud.MessageType.TopLeft, quick
                        ? "Quickstack: brak przedmiotów do przeniesienia."
                        : "Restock: brak niepełnych stosów.");
                    yield break;
                }

                // Co-operative work: no arbitrary 128-chest truncation, no
                // full-scene iteration; each frame gets at most 2ms of work.
                for (int ci=0;ci<Nearby.Count;ci++)
                {
                    // Yield even when a chest has no relevant items.
                    // Thousands of unrelated chests must never run in a
                    // single frame simply because no ownership RPC occurred.
                    if (Budget.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                    {
                        yield return null;
                        Budget.Restart();
                    }
                    if (quick && QolAzuAutoStoreCompatibility.UseAzuQuickStack) yield break;
                    Container chest = Nearby[ci];
                    if (player == null || player != Player.m_localPlayer) yield break;
                    if (chest == null || !CanAccess(chest,player)) { skipped++; continue; }
                    Inventory dest = QolGameAccess.Inventory(chest);
                    if (dest == null || dest == src) continue;

                    // MAJOR optimization: scan the chest BEFORE ClaimOwnership.
                    // If none of its contents are needed, no RPC/ZDO changes.
                    if (!Relevant(dest,quick)) continue;
                    if (Budget.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                    {
                        yield return null;
                        Budget.Restart();
                    }
                    ZNetView view = QolGameAccess.View(chest);
                    if (view == null) continue;
                    if (!view.IsOwner())
                    {
                        view.ClaimOwnership();
                        float deadline = Time.realtimeSinceStartup+.9f;
                        while (view != null && !view.IsOwner() && Time.realtimeSinceStartup < deadline)
                            yield return null;
                        Budget.Restart();
                        if (view == null || !view.IsOwner()) { skipped++; continue; }
                    }
                    if (!CanAccess(chest,player)) { skipped++; continue; }
                    // Ownership handshake may have changed the chest inventory.
                    if (!Relevant(dest,quick)) continue;

                    int movedChest = 0;
                    for (int pi=0;pi<PlayerItems.Count;pi++)
                    {
                        ItemDrop.ItemData own = PlayerItems[pi];
                        if (own == null || own.m_shared == null || !src.ContainsItem(own)) continue;
                        if (!Contents.TryGetValue(own.m_shared.m_name,
                            out List<ItemDrop.ItemData> matches)) continue;
                        if (quick)
                        {
                            if (QolFavorites.IsFavorite(own,src)) continue;
                            int before = own.m_stack;
                            dest.MoveItemToThis(src,own);
                            int delta = src.ContainsItem(own)
                                ? Math.Max(0,before-own.m_stack) : before;
                            movedChest += delta;
                            if (delta > 0)
                            {
                                string name = own.m_shared.m_name;
                                if (firstItem == null) firstItem = name;
                                else if (!string.Equals(firstItem, name, StringComparison.Ordinal))
                                    multipleTypes = true;
                            }
                        }
                        else
                        {
                            if (own.m_customData != null && own.m_customData.Count > 0) continue;
                            int missing = own.m_shared.m_maxStackSize - own.m_stack;
                            if (missing <= 0) continue;
                            for (int idx=matches.Count-1; idx>=0 && missing>0;idx--)
                            {
                                ItemDrop.ItemData item = matches[idx];
                                if (item == null || item.m_stack <= 0 ||
                                    item.m_quality != own.m_quality ||
                                    item.m_customData != null && item.m_customData.Count>0) continue;
                                int count = Math.Min(missing,item.m_stack);
                                if (count > 0 && src.MoveItemToThis(dest,item,count,
                                    own.m_gridPos.x,own.m_gridPos.y))
                                {
                                    movedChest += count;
                                    missing -= count;
                                }
                                // Budget includes all match checks, even failed transfers.
                                if (Budget.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                                {
                                    yield return null;
                                    Budget.Restart();
                                }
                            }
                        }
                        if (Budget.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                        {
                            yield return null;
                            Budget.Restart();
                        }
                    }

                    if (movedChest > 0)
                    {
                        moved += movedChest;
                        touched++;
                        if (firstModified == null) firstModified = chest;
                        QolGameAccess.Save(chest); // once per changed chest
                        if (quick) PingQuickStackChest(chest); // once per modified destination
                    }
                    ClearContentIndex();
                    if (Budget.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                    {
                        yield return null;
                        Budget.Restart();
                    }
                }
                if (quick && moved > 0)
                {
                    string text = !multipleTypes && firstItem != null
                        ? "Przeniesiono " +
                          LocalizeItem(firstItem) +
                          " (x" + moved + ") do pobliskiej skrzyni."
                        : "Przeniesiono " + moved + " przedmiotów do pobliskich skrzyń.";
                    player.Message(MessageHud.MessageType.TopLeft, text);
                }
                else
                    player.Message(MessageHud.MessageType.TopLeft,
                        (quick ? "Quickstack: " : "Restock: ") + moved +
                        " szt. / " + touched + " skrzyń" +
                        (skipped>0 ? " (pominięto: "+skipped+")" : ""));
                if (moved > 0 && !quick) PlayFeedback(firstModified,player);
            }
            finally
            {
                Budget.Stop();
                _busy = false;
                Nearby.Clear();
                Wanted.Clear();
                PlayerItems.Clear();
                ClearContentIndex();
            }
        }

        private static void GetContainers(Player player)
        {
            Nearby.Clear();
            Vector3 origin = player.transform.position;
            int cx = Mathf.FloorToInt(origin.x / CellSize);
            int cz = Mathf.FloorToInt(origin.z / CellSize);
            // 100m reach + a 32m cell edge: at most 9x9 occupied
            // hash buckets instead of walking millions of distant containers.
            for(int dx=-4; dx<=4; dx++)
                for(int dz=-4; dz<=4; dz++)
                {
                    if (!Cells.TryGetValue(Key(cx+dx,cz+dz),
                        out List<Container> bucket)) continue;
                    for(int i=bucket.Count-1;i>=0;i--)
                    {
                        Container chest = bucket[i];
                        if (chest == null) { bucket.RemoveAt(i); continue; }
                        if (!chest.gameObject.activeInHierarchy) continue;
                        // If a movable chest shifted buckets, update its index
                        // on the next hotkey operation instead of scanning every frame.
                        if ((chest.transform.position-origin).sqrMagnitude <= RangeSq)
                            Nearby.Add(chest);
                    }
                }
            // No Nearby.Sort: the bucket iteration already gives a bounded
            // spatial walk, and sorting >1000 transforms was a long frame.
        }

        private static bool CanAccess(Container chest, Player player)
        {
            if (chest == null || player == null || QolGameAccess.View(chest) == null || !QolGameAccess.View(chest).IsValid() ||
                QolGameAccess.Inventory(chest) == null || QolGameAccess.View(chest).GetZDO() == null) return false;
            if (!QolGameAccess.CheckAccess(chest, player.GetPlayerID())) return false;
            if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, true, false))
                return false;
            bool openedLocally = InventoryGui.instance != null &&
                QolGameAccess.CurrentContainer(InventoryGui.instance) == chest;
            if (!openedLocally && (chest.IsInUse() ||
                QolGameAccess.View(chest).GetZDO().GetInt("InUse", 0) == 1)) return false;
            if (QolGameAccess.Piece(chest) != null && !QolGameAccess.Piece(chest).IsPlacedByPlayer()) return false;
            // On shared worlds, skip ship containers: their ownership and
            // interpolation can change while a remote batch is underway.
            if (chest.GetComponentInParent<Ship>() != null) return false;
            return true;
        }

        private static MethodInfo FindLocalizationMethod()
        {
            if (LocalizationType == null) return null;
            foreach (MethodInfo method in LocalizationType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "Localize") continue;
                ParameterInfo[] args = method.GetParameters();
                if (args.Length == 1 && args[0].ParameterType == typeof(string)) return method;
            }
            return null;
        }

        private static string LocalizeItem(string name)
        {
            if (LocalizationMethod == null || LocalizationSingleton == null) return name;
            try
            {
                object locale = LocalizationSingleton.GetValue(null);
                return locale == null ? name :
                    LocalizationMethod.Invoke(locale, new object[] { name }) as string ?? name;
            }
            catch { return name; }
        }

        private static bool _missingQuickVfxReported;

        private static void PingQuickStackChest(Container chest)
        {
            if (chest == null) return;
            // Azu's PingContainer() silently does NOTHING if its separate
            // Ping Containers setting is disabled or an existing ping component
            // is still alive. Sirroh's explicit QuickStack must always show
            // feedback on every modified destination, independent of Azu.
            try
            {
                if (ZNetScene.instance != null)
                {
                    if (_pingPrefab == null)
                        _pingPrefab = ZNetScene.instance.GetPrefab("vfx_Potion_health_medium");
                    if (_pingPrefab != null)
                    {
                        // Use the SAME source prefab as Azu, but spawn the effect
                        // directly at the successful destination, not via Azu's
                        // configuration-gated optional component.
                        GameObject visual = UnityEngine.Object.Instantiate(_pingPrefab,
                            chest.transform.position + Vector3.up * 0.35f,
                            Quaternion.identity);
                        if (visual != null) UnityEngine.Object.Destroy(visual, 10f);
                    }
                    else if (!_missingQuickVfxReported)
                    {
                        _missingQuickVfxReported = true;
                        _host?.Log.LogWarning("QuickStack VFX: vfx_Potion_health_medium prefab unavailable.");
                    }
                }

                // Azu also highlights the chest for several seconds. Add an
                // equivalent short-lived native WearNTear highlight on demand.
                QolChestPingHighlight glow = chest.GetComponent<QolChestPingHighlight>();
                if (glow == null) glow = chest.gameObject.AddComponent<QolChestPingHighlight>();
                glow.Trigger();

                // Native move-item effect, once per changed container.
                InventoryGui gui = InventoryGui.instance;
                if (gui != null && QolGameAccess.CurrentContainer(gui) != chest)
                    gui.m_moveItemEffects.Create(chest.transform.position,
                        Quaternion.identity, null, 1f, -1, default(ZDOID));
            }
            catch (Exception error)
            {
                _host?.Log.LogWarning("QuickStack destination VFX: " + error.Message);
            }
        }

        private static void PlayFeedback(Container source, Player player)
        {
            // Reuse the game's native container opening SFX/VFX, as in the
            // referenced mod, but only ONCE after a successful hotkey batch.
            // Never open and close hundreds of remote chests just for effects.
            if (source == null || player == null || ContainerOpenEffects == null) return;
            try
            {
                object effects = ContainerOpenEffects.GetValue(source);
                if (effects == null) return;
                MethodInfo create = null;
                foreach (MethodInfo method in effects.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (method.Name != "Create") continue;
                    ParameterInfo[] args = method.GetParameters();
                    if (args.Length >= 2 && args[0].ParameterType == typeof(Vector3) &&
                        args[1].ParameterType == typeof(Quaternion)) { create = method; break; }
                }
                if (create == null) return;
                ParameterInfo[] parameters = create.GetParameters();
                object[] values = new object[parameters.Length];
                values[0] = player.transform.position;
                values[1] = Quaternion.identity;
                for (int i=2; i<values.Length; i++)
                {
                    Type type = parameters[i].ParameterType;
                    values[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue :
                        type.IsValueType ? Activator.CreateInstance(type) : null;
                }
                create.Invoke(effects, values);
            }
            catch (Exception e)
            {
                _host?.Log.LogDebug("QOL transfer effect unavailable: " + e.Message);
            }
        }

        internal static void Shutdown()
        {
            _host = null; _busy = false;
            Locations.Clear(); Cells.Clear(); Nearby.Clear();
            Wanted.Clear(); PlayerItems.Clear(); ClearContentIndex(); ListPool.Clear();
        }
    }
    // Active only for a few seconds after a successful QuickStack deposit.
    // No additional scanning, polling or persistent Update components.
    internal sealed class QolChestPingHighlight : MonoBehaviour
    {
        private WearNTear _wear;
        private float _until;

        private void Awake() { _wear = GetComponent<WearNTear>(); }
        internal void Trigger() { _until = Time.time + 4f; }

        private void Update()
        {
            if (Time.time >= _until) { Destroy(this); return; }
            if (_wear != null) _wear.Highlight();
        }
    }

    internal sealed class QolChestTracker : MonoBehaviour
    {
        private Container _container;
        internal void Bind(Container container) { _container = container; }
        private void OnDestroy()
        {
            if (_container != null) QolNearbyInventory.OnContainerDestroy(_container);
            _container = null;
        }
    }
}
